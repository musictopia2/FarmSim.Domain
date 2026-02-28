using System.Threading.Channels;

namespace FarmSim.Domain.Services.Automation.Worksites;
public sealed class WorksiteAutomationManager(
    InventoryManager inventory,
    BalanceManager balanceManager,
    RulesManager rulesManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager
) : IWorksiteManager
{
    private readonly Lock _lock = new();
    private bool _init;
    private IWorksiteAutomationProfile _profile = null!;
    private IWorksiteBaseRulesProvider _baseRulesProvider = null!;
    private IWorkerRepository _workerRepository = null!;
    private BasicList<WorksiteAutomationStateModel> _worksites = [];
    private BasicList<WorkerRecipe> _allWorkers = [];
    private BasicList<WorksiteRecipe> _worksiteRecipes = [];
    private BasicList<UnlockModel> _workerStates = [];
    private Dictionary<string, WorksiteBaseRuleModel> _rulesByLocation = new(StringComparer.OrdinalIgnoreCase);

    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;

    // ------------------------------------------------------------
    // Init
    // ------------------------------------------------------------

    public async Task SetStyleContextAsync(WorksiteAutomationServicesContext worksiteContext,
        WorkerServicesContext workerContext, FarmKey farm
        )
    {
        if (rulesManager.AutomationEnabled == false)
        {
            return; //don't do anything because no automation.  needs to double check.
        }
        // context contains the “manual” service set historically, but we’re only consuming
        // recipes, workers, and automation profile/rules provider from it.
        _profile = worksiteContext.WorksiteAutomationProfile;
        _baseRulesProvider = worksiteContext.WorksiteBaseRulesProvider;
        _workerStates = await workerContext.WorkerRepository.LoadAsync();
        _workerRepository = workerContext.WorkerRepository;
        _worksiteRecipes = await worksiteContext.WorksiteRecipes.GetWorksitesAsync();
        _allWorkers = await workerContext.WorkerRecipes.GetWorkersAsync();
        foreach (var item in _worksiteRecipes)
        {
            foreach (var temp in item.BaselineBenefits)
            {
                EnumInventoryStorageCategory category;
                if (temp.Optional)
                {
                    category = EnumInventoryStorageCategory.None;
                }
                else
                {
                    category = EnumInventoryStorageCategory.Barn;
                }
                itemRegistry.Register(new(temp.Item, category, EnumInventoryItemCategory.Worksites));
            }
        }
        var rules = await _baseRulesProvider.GetRulesAsync(farm);
        _rulesByLocation = rules.ToDictionary(x => x.Location, StringComparer.OrdinalIgnoreCase);

        var loaded = await _profile.LoadAsync();
        _worksites = loaded ?? [];

        // ensure there is a state entry for every location we have recipes for
        EnsureAllLocationsExist_NoLock();

        _init = true;
    }

    private void EnsureAllLocationsExist_NoLock()
    {
        foreach (var r in _worksiteRecipes)
        {
            if (_worksites.Any(x => x.Location.Equals(r.Location, StringComparison.OrdinalIgnoreCase)) == false)
            {
                _worksites.Add(new WorksiteAutomationStateModel
                {
                    Location = r.Location,
                    Unlocked = false,
                    Workers = [],
                    Rewards = [],
                    FailureHistory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    RequestedTotal = 0,
                    DeliveredTowardRequest = 0,
                    BlockedAt = null
                });
                _needsSaving = true;
            }
        }

        // normalize null collections for safety
        foreach (var s in _worksites)
        {
            s.Workers ??= [];
            s.Rewards ??= [];
            s.FailureHistory ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------
    // Public helpers
    // ------------------------------------------------------------


    public BasicList<string> GetWorkerActiveLocations(string workerName)
    {
        EnsureInit();
        lock (_lock)
        {
            BasicList<string> output = [];
            foreach (var s in _worksites)
            {
                if (s.Workers.Any(w => w.WorkerName.Equals(workerName, StringComparison.OrdinalIgnoreCase)))
                {
                    output.Add(s.Location);
                }
            }
            return output;
        }
    }

    public BasicList<string> GetWorkerPlannedLocations(string workerName)
    {
        EnsureInit();
        lock (_lock)
        {
            BasicList<string> output = [];
            foreach (var s in _worksites)
            {
                if (s.PendingWorkers.Any(w => w.WorkerName.Equals(workerName, StringComparison.OrdinalIgnoreCase)))
                {
                    output.Add(s.Location);
                }
            }
            return output;
        }
    }

    public void CompleteAllJobsImmediately()
    {
        if (inventory.Has(CurrencyKeys.FinishAllWorksites, 1) == false)
        {
            throw new CustomBasicException("You do not have any Finish All Worksites consumabes left.  Should had called inventory.Has function");
        }
        foreach (var item in _worksites)
        {
            CompleteSingleSiteJob(item);
        }
        inventory.Consume(CurrencyKeys.FinishAllWorksites, 1);
    }
    public void CompleteSingleWorksiteImmediately(string location)
    {
        if (inventory.Has(CurrencyKeys.FinishSingleWorksite, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish single worksite consumables left.  Should had called inventory.Has function");
        }
        WorksiteAutomationStateModel site = GetWorksite(location);
        CompleteSingleSiteJob(site);
        inventory.Consume(CurrencyKeys.FinishSingleWorksite, 1);
    }
    private void CompleteSingleSiteJob(WorksiteAutomationStateModel worksite)
    {
        lock (_lock)
        {
            DateTime now = DateTime.Now;
            CompleteSingleRun_NoLock(worksite, now);
        }
    }
    public void UsePowerGlove(string location, int howMany)
    {
        if (howMany <= 0)
        {
            return;
        }
        if (inventory.Has(CurrencyKeys.PowerGloveWorksite, howMany) == false)
        {
            throw new CustomBasicException("Don't have enough power gloves.  Should had called the inventorymanager.Has function");
        }

        // Apply may decide to consume fewer (or none) if there is nothing meaningful to reduce.
        int consumed = ApplyPowerGloveToWorksite(location, howMany, PowerGloveRegistry.ReduceBy);
        if (consumed > 0)
        {
            inventory.Consume(CurrencyKeys.PowerGloveWorksite, consumed);
        }
    }

    /// <summary>
    /// Applies a batch of power gloves to a single active run.
    /// IMPORTANT RULES (matches manual behavior):
    /// - If the reduction would finish the current run, complete EXACTLY ONE run.
    /// - Any leftover reduction is wasted; it does NOT roll into the next run.
    /// - After completing one run, we may start the next run normally (consuming inputs) if possible.
    /// </summary>
    private int ApplyPowerGloveToWorksite(string location, int used, TimeSpan reduceByPerUse)
    {
        if (used <= 0)
        {
            return 0;
        }

        TimeSpan totalReduce = reduceByPerUse * used;

        lock (_lock)
        {
            WorksiteAutomationStateModel worksite = _worksites.Single(x => x.Location == location);

            // Nothing to do if not unlocked
            if (worksite.Unlocked == false)
            {
                return 0;
            }

            // If blocked or has rewards pending, we don't allow gloves to "skip" the block.
            if (worksite.BlockedAt is not null || worksite.Rewards.Count > 0)
            {
                return 0;
            }

            int outstanding = Math.Max(0, worksite.RequestedTotal - worksite.DeliveredTowardRequest);
            if (outstanding <= 0)
            {
                return 0;
            }

            // Must be actively running to reduce time.
            if (worksite.StartedAt is null)
            {
                return 0;
            }

            var duration = GetRunDuration_NoLock(location);
            if (duration <= TimeSpan.Zero)
            {
                return 0;
            }

            var now = DateTime.Now;
            var elapsed = now - worksite.StartedAt.Value;
            var remaining = duration - elapsed;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            // If reduction completes the run, complete exactly ONE run and waste leftover time.
            if (totalReduce >= remaining)
            {
                CompleteSingleRun_NoLock(worksite, now);

                // After completing one run, try to start the next run normally.
                // Any leftover reduction is intentionally wasted.
                if (worksite.BlockedAt is null)
                {
                    TryStartIfPossible_NoLock(worksite, now);
                }
            }
            else
            {
                // Reduce remaining time by pretending we started earlier.
                worksite.StartedAt = worksite.StartedAt.Value - totalReduce;
            }

            _needsSaving = true;
            return used;
        }
    }

    private void CompleteSingleRun_NoLock(WorksiteAutomationStateModel s, DateTime now)
    {
        // Defensive: nothing to do if already blocked or no outstanding
        int outstanding = Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
        if (outstanding <= 0)
        {
            s.StartedAt = null;
            return;
        }

        // Complete exactly ONE run
        s.DeliveredTowardRequest += 1;
        s.StartedAt = null;

        var produced = GenerateRewardsForCompletedRun_NoLock(s);

        // augmentation extras (if promised)
        var aug = GetWorksiteAugmentationRewards_NoLock(s);
        foreach (var ia in aug)
        {
            produced.Add(ia);
        }

        // promise consumed per run
        ClearAugmentationPromise_NoLock(s);

        ReleaseWorkers(s);
        foreach (var ia in produced)
        {
            s.Rewards.Add(ia);
        }

        // deposit immediately; if blocked, latch and stop
        if (s.Rewards.Count > 0)
        {
            bool cleared = TryDepositStoredRewards_NoLock(s);
            if (cleared)
            {
                s.BlockedAt = null;
            }
            else
            {
                s.BlockedAt ??= now;
            }
        }
    }
    public bool IsBlocked(string location)
    {
        var worksite = GetWorksite(location);
        return worksite.BlockedAt is not null;
    }

    public string? GetPossibleWorksiteForItem(string name)
    {
        foreach (var worksite in _worksites)
        {
            var recipe = GetRecipe_NoLock(worksite.Location);
            if (recipe.BaselineBenefits.Exists(x => x.Item == name))
            {
                return worksite.Location;
            }
        }
        return null;
    }

    public BasicList<ItemAmount> SuppliesNeeded(string location)
    {
        if (timedBoostManager.HasNoSuppliesNeededForWorksites())
        {
            return [];
        }
        var recipe = GetRecipe_NoLock(location);
        BasicList<ItemAmount> output = [];
        foreach (var item in recipe.Inputs)
        {
            output.Add(new()
            {
                Amount = item.Value,
                Item = item.Key
            });
        }
        return output;
    }
    public int GetOutstandingRuns(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            var s = GetWorksite(location);
            return Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
        }
    }
    public int GetQueueCap(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            if (_rulesByLocation.TryGetValue(location, out var rule) == false)
            {
                return 0;
            }
            return rule.StartingQueueCount + advancedUpgradeAutomationManager.ExtraQueCountBenefit(location);
        }
    }
    public int GetRemainingQueueSpace(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            var cap = GetQueueCap(location);
            var outstanding = GetOutstandingRuns_NoLock(location);
            return Math.Max(0, cap - outstanding);
        }
    }

    public int GetMaximumWorkers(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            var r = GetRecipe_NoLock(location);
            return r.MaximumWorkers;
        }
    }
    //location does not matter for this now
    public BasicList<WorkerRecipe> GetUnlockedWorkers()
    {
        var unlockedNames = _workerStates.Where(x => x.Unlocked).ToBasicList();
        BasicList<WorkerRecipe> output = [];
        foreach (var item in unlockedNames)
        {
            if (item.Unlocked)
            {
                var recipe = _allWorkers.Single(x => x.WorkerName == item.Name);
                output.Add(recipe); //i think.
            }
        }
        return output;
    }
    public BasicList<string> GetUnlockedWorksites()
    {
        BasicList<string> output = [];
        _worksites.ForConditionalItems(x => x.Unlocked, t =>
        {
            output.Add(t.Location);
        });
        return output;
    }

    public bool IsUnlocked(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            return GetWorksite(location).Unlocked;
        }
    }
    private void KickAwake_NoLock(WorksiteAutomationStateModel s)
    {
        // Only meaningful if there is outstanding work
        int outstanding = Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
        if (outstanding <= 0)
        {
            return;
        }

        // Clear the blocked latch so the next tick is allowed to try
        s.BlockedAt = null;

        // If we’re not running yet, ensure we are in a clean “idle” state
        if (s.StartedAt is null)
        {
            // if you ever store other per-run locks later, clear them here too
        }

        _needsSaving = true;
    }
    public bool IsRunning(string location)
    {
        var item = _worksites.Single(x => x.Location == location);
        if (item.StartedAt is null)
        {
            return false;
        }
        if (item.BlockedAt is not null)
        {
            return false; //even now not running.
        }
        return true; //for now.
    }
    public void RequestRuns(string location, int runs)
    {
        EnsureInit();
        if (runs <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var s = GetWorksite(location);
            if (s.Unlocked == false)
            {
                return;
            }

            int space = GetRemainingQueueSpace_NoLock(location);
            if (space <= 0)
            {
                return;
            }

            bool wasInactive =
                (Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest) <= 0)
                && (s.StartedAt is null)
                && (s.Rewards.Count == 0);

            int add = Math.Min(space, runs);
            s.RequestedTotal += add;

            if (wasInactive)
            {
                KickAwake_NoLock(s);
                // Optional: if you want “commit pending immediately” so tick can start:
                if (s.PendingWorkers.Count > 0)
                {
                    CommitPendingWorkers_NoLock(s);
                }
            }

            _needsSaving = true;
        }
    }

    public TimeSpan? GetTimeUntilNextReady(string location)
    {
        EnsureInit();
        lock (_lock)
        {
            var s = GetWorksite(location);
            if (s.Unlocked == false)
            {
                return null;
            }

            // If blocked by undeposited rewards, no next ready
            if (s.Rewards.Count > 0)
            {
                return null;
            }

            int outstanding = Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
            if (outstanding <= 0)
            {
                return null;
            }

            var duration = GetRunDuration_NoLock(location);

            if (s.StartedAt is null)
            {
                return duration; // not started yet
            }

            var elapsed = DateTime.Now - s.StartedAt.Value;
            var remaining = duration - elapsed;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            return remaining;
        }
    }
    public BasicList<WorkerRecipe> GetPendingWorkers(string location)
    {
        var s = GetWorksite(location);
        return s.PendingWorkers.ToBasicList();
    }
    public BasicList<WorkerRecipe> GetAssignedWorkers(string location)
    {
        var s = GetWorksite(location);
        return s.Workers.ToBasicList();
    }
    public void AddWorker(string location, WorkerRecipe worker)
    {
        lock (_lock)
        {
            var s = GetWorksite(location);
            var r = GetRecipe_NoLock(location);
            foreach (var item in _worksites)
            {
                item.PendingWorkers.RemoveAllOnly(x => x.WorkerName == worker.WorkerName);
            }
            if (s.PendingWorkers.Count == r.MaximumWorkers)
            {
                s.PendingWorkers.RemoveFirstItem();
            }
            s.PendingWorkers.Add(worker);
            _needsSaving = true;
        }

    }

    public void RemoveWorker(string location, WorkerRecipe worker)
    {
        lock (_lock)
        {
            var s = GetWorksite(location);
            s.PendingWorkers.RemoveAllOnly(x => x.WorkerName == worker.WorkerName);
            _needsSaving = true;
        }

    }


    // ------------------------------------------------------------
    // Tick
    // ------------------------------------------------------------

    private void UpdateTick(WorksiteAutomationStateModel s, DateTime now)
    {
        // 0) If there are outstanding requests but job isn't started, workers should be committed
        //    (You currently require Workers.Count > 0 to run, but you only add PendingWorkers.)
        //    Minimal: if idle and no workers, try to promote pending -> workers.
        int outstanding = Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);

        if (outstanding > 0
            && s.StartedAt is null
            && s.Rewards.Count == 0
            && s.Workers.Count == 0
            && s.PendingWorkers.Count > 0)
        {
            CommitPendingWorkers_NoLock(s);
        }
        // 1) If rewards exist, try to deposit. This is ALWAYS highest priority.
        if (s.Rewards.Count > 0)
        {
            bool cleared = TryDepositStoredRewards_NoLock(s);
            if (cleared)
            {
                // Unblocked ONLY if that was the reason. Clear latch and continue processing.
                s.BlockedAt = null;

                // After clearing rewards, do NOT catch up; just try to start normally.
                TryStartIfPossible_NoLock(s, now);
            }
            else
            {
                s.BlockedAt ??= now; // latch
            }
            return;
        }

        // 2) No rewards; compute outstanding
        if (outstanding <= 0)
        {
            if (s.StartedAt is not null)
            {
                _needsSaving = true;
            }
            if (s.BlockedAt is not null)
            {
                _needsSaving = true;
            }
            s.StartedAt = null;
            s.BlockedAt = null;
            return;
        }

        // 3) If currently blocked (for any reason), do ONLY "unblock attempts", no catch-up.
        if (s.BlockedAt is not null)
        {
            // blocked-start case (no rewards, so it's either workers or inputs).
            // Try to unblock by attempting start.
            if (s.Workers.Count == 0 && s.PendingWorkers.Count > 0)
            {
                CommitPendingWorkers_NoLock(s);
            }

            bool started = TryStartIfPossible_NoLock(s, now);
            if (started)
            {
                // started => unblocked
                s.BlockedAt = null;
                _needsSaving = true;
            }

            return;
        }

        // 4) Not blocked and not started: try to start immediately
        if (s.StartedAt is null)
        {
            bool started = TryStartIfPossible_NoLock(s, now);
            if (started == false)
            {
                // Can't start => enter blocked latch (no catch-up until unblocked)
                if (s.BlockedAt is null)
                {
                    _needsSaving = true;
                }
                s.BlockedAt ??= now;
            }
            return;
        }

        // 5) Started and not blocked: normal progression + catch-up allowed
        var duration = GetRunDuration_NoLock(s.Location);
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var elapsed = now - s.StartedAt.Value;
        int runsReady = (int)Math.Floor(elapsed.TotalSeconds / duration.TotalSeconds);
        if (runsReady <= 0)
        {
            return;
        }

        // clamp to outstanding
        runsReady = Math.Min(runsReady, outstanding);

        // catch-up loop: stops if we hit inventory block at completion
        for (int i = 0; i < runsReady; i++)
        {
            // complete one run
            s.DeliveredTowardRequest += 1;

            // run finished -> reset timer
            s.StartedAt = null;

            var produced = GenerateRewardsForCompletedRun_NoLock(s);

            // ✅ add augmentation extras (if promised)
            var aug = GetWorksiteAugmentationRewards_NoLock(s);
            foreach (var ia in aug)
            {
                produced.Add(ia);
            }

            // ✅ promise consumed per run
            ClearAugmentationPromise_NoLock(s);

            ReleaseWorkers(s);
            foreach (var ia in produced)
            {
                s.Rewards.Add(ia);
            }

            // deposit immediately
            if (s.Rewards.Count > 0)
            {
                bool cleared = TryDepositStoredRewards_NoLock(s);
                if (cleared)
                {
                    s.BlockedAt = null; // ensure cleared
                }
                else
                {
                    // inventory blocked at end-of-run => latch and stop (no catch-up further)
                    if (s.BlockedAt is null)
                    {
                        _needsSaving = true;
                    }
                    s.BlockedAt ??= now;
                    return;
                }
            }

            // try to start next run immediately (still in same tick)
            if (i < runsReady - 1) // only if we'd otherwise continue catching up
            {
                bool started = TryStartIfPossible_NoLock(s, now);
                if (started == false)
                {
                    // can't start next run => latch and stop (no more catch-up)
                    if (s.BlockedAt is null)
                    {
                        _needsSaving = true;
                    }
                    s.BlockedAt ??= now;
                    return;
                }
            }
        }

        _needsSaving = true;
    }
    private bool TryStartIfPossible_NoLock(WorksiteAutomationStateModel s, DateTime now)
    {
        if (s.StartedAt is not null)
        {
            return true;
        }

        int outstanding = Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
        if (outstanding <= 0)
        {
            return false;
        }

        if (s.Workers.Count == 0 && s.PendingWorkers.Count > 0)
        {
            CommitPendingWorkers_NoLock(s);
        }
        if (s.Workers.Count == 0)
        {
            return false;
        }

        if (CanConsumeInputsForRun_NoLock(s.Location) == false)
        {
            return false;
        }

        ConsumeInputsForRun_NoLock(s.Location);

        // ✅ lock promise per run (manual-equivalent behavior)
        TryLockAugmentationPromise_NoLock(s);

        s.StartedAt = now;
        _needsSaving = true;
        return true;
    }
    private void CommitPendingWorkers_NoLock(WorksiteAutomationStateModel s)
    {
        if (s.StartedAt is not null)
        {
            return;
        }
        var recipe = GetRecipe_NoLock(s.Location);

        // Keep it simple: desired list is PendingWorkers; commit up to max.
        s.Workers.Clear();

        int max = recipe.MaximumWorkers;
        if (max <= 0)
        {
            max = 1;
        }

        foreach (var w in s.PendingWorkers)
        {
            if (IsWorkerUnlockedNow(w.WorkerName))
            {
                s.Workers.Add(w);
            }
            if (s.Workers.Count >= max)
            {
                break;
            }
        }
        _needsSaving = true;
    }


    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }
        BasicList<WorksiteAutomationStateModel>? toSave = null;
        lock (_lock)
        {
            var now = DateTime.Now;
            _worksites.ForConditionalItems(x => x.Unlocked, x => UpdateTick(x, now));
            if (ShouldSave_NoLock())
            {
                toSave = _worksites;
                _needsSaving = false;
                _lastSave = now;
            }
        }
        if (toSave is not null)
        {
            await _profile.SaveAsync(toSave);
        }
    }
    private bool ShouldSave_NoLock()
    {
        if (_needsSaving == false)
        {
            return false;
        }

        return (DateTime.Now - _lastSave) > TimeSpan.FromSeconds(2);
    }

    private BasicList<WorkerRecipe> GetWorkersForPreview(
    WorksiteAutomationStateModel worksite,
    EnumWorksitePreviewMode mode)
    {
        return mode switch
        {
            EnumWorksitePreviewMode.UIWorkersActive =>
                worksite.Workers,
            EnumWorksitePreviewMode.AutomatedActiveWorkers =>
                worksite.Workers,
            EnumWorksitePreviewMode.PlannedUnlockedWorkers =>
                worksite.PendingWorkers
                    .Where(w => IsWorkerUnlockedNow(w.WorkerName) == true)
                    .ToBasicList(),

            EnumWorksitePreviewMode.PlannedAllWorkers =>
                worksite.PendingWorkers,

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    // ------------------------------------------------------------
    // Reward generation (multi-reward, unknown results)
    // ------------------------------------------------------------


    public BasicList<WorksiteRewardPreview> GetPreview(string location, EnumWorksitePreviewMode mode)
    {
        BasicList<WorksiteRewardPreview> output = [];
        WorksiteRewardPreview preview;
        var recipe = GetRecipe_NoLock(location);
        var worksite = GetWorksite(location);
        var workers = GetWorkersForPreview(worksite, mode);
        if (workers.Count == 0)
        {
            foreach (var firsts in recipe.BaselineBenefits)
            {
                preview = new()
                {
                    Chance = 0,
                    Amount = firsts.Quantity,
                    Item = firsts.Item
                };
                output.Add(preview);
            }
            return output;
        }


        HashSet<string> candidateItems = [];

        foreach (var b in recipe.BaselineBenefits)
        {
            candidateItems.Add(b.Item);
        }
        foreach (var worker in workers)
        {
            foreach (var benefit in worker.Benefits)
            {
                if (benefit.Worksite == recipe.Location)
                {
                    candidateItems.Add(benefit.Item);
                }
            }
        }

        foreach (var review in candidateItems)
        {
            WorksiteBaselineBenefit? startBenefit = recipe.BaselineBenefits.SingleOrDefault(x => x.Item == review);
            BasicList<WorkerBenefit> workerBenefits = GetWorkerBenefits(workers, location, review);
            if (workerBenefits.Count == 0 && startBenefit is null)
            {
                continue;
            }

            int amount = startBenefit?.Quantity ?? 0;

            if (workerBenefits.Any(x => x.GivesExtra))
            {
                amount++;
            }
            else if (startBenefit is not null)
            {
                if (startBenefit.EachWorkerGivesOne)
                {
                    amount = startBenefit.Quantity * workers.Count;
                }
            }
            else
            {
                amount = 1;
            }

            double chances = 0;
            if (startBenefit is not null && startBenefit.Guarantee)
            {
                chances = 1;
            }
            else if (workerBenefits.Any(x => x.Guarantee))
            {
                chances = 1;
            }
            else
            {
                double startBase = startBenefit?.Chance ?? 0;

                if (worksite.FailureHistory.TryGetValue(review, out int times))
                {
                    // Hard drought cap: on the 4th failure, guarantee it next time
                    // (only for non-optional items; optional bonus loot stays unaffected)
                    bool isOptional = startBenefit?.Optional ?? true;
                    if (isOptional == false && times >= 4) //every 4 time you get the base items no matter what.
                    {
                        chances = 1;
                    }
                    else if (isOptional == false)
                    {
                        startBase += 0.08 * times;
                    }
                }
                if (chances < 1)
                {
                    double baseChance = startBase * workers.Count;
                    double extras = workerBenefits.Sum(x => x.ChanceModifier);
                    chances = baseChance + extras;
                }

            }

            if (chances > 1)
            {
                chances = 1;
            }

            if (chances == 0)
            {
                throw new CustomBasicException($"Must have at least a small chance or why bother including {review}");
            }
            if (mode != EnumWorksitePreviewMode.AutomatedActiveWorkers)
            {
                chances *= 100;
            }
            bool optional;
            if (startBenefit is null)
            {
                optional = true;
            }
            else
            {
                optional = startBenefit.Optional;
            }

            preview = new()
            {
                Item = review,
                Amount = amount,
                Chance = chances,
                Optional = optional
            };
            output.Add(preview);
        }

        if (mode != EnumWorksitePreviewMode.AutomatedActiveWorkers)
        {
            //needs to do the extra rewards too.
            //
            string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(worksite.Location);
            if (key is not null)
            {
                worksite.OutputPromise = outputAugmentationManager.GetSnapshot(key);
                var aug = GetWorksiteAugmentationRewards_NoLock(worksite);
                foreach (var ia in aug)
                {
                    preview = new()
                    {
                        Item = ia.Item,
                        Amount = ia.Amount,
                        Chance = 100,
                        Optional = true
                    };
                    output.Add(preview);
                }
            }
        }
        return output;
    }
    private static BasicList<WorkerBenefit> GetWorkerBenefits(
        BasicList<WorkerRecipe> workers,
        string location,
        string item)
    {
        BasicList<WorkerBenefit> output = [];

        foreach (var worker in workers)
        {
            // In automation mode CurrentLocation isn't set.
            // The benefit itself knows which worksite it applies to.
            output.AddRange(worker.Benefits.Where(b =>
                b.Item.Equals(item, StringComparison.OrdinalIgnoreCase) &&
                b.Worksite.Equals(location, StringComparison.OrdinalIgnoreCase)));
        }

        return output;
    }

    private BasicList<ItemAmount> GenerateRewardsForCompletedRun_NoLock(WorksiteAutomationStateModel state)
    {
        //var recipe = GetRecipe_NoLock(state.Location);

        BasicList<WorksiteRewardPreview> list = GetPreview(state.Location, EnumWorksitePreviewMode.AutomatedActiveWorkers);
        BasicList<ItemAmount> output = [];
        foreach (var item in list)
        {
            if (ShouldAward(item.Chance))
            {
                output.Add(new(item.Item, item.Amount));
                // Always clear on success (even if we weren't tracking this item yet).
                // This keeps the rule "success resets pity" true even if tracking rules change later.
                state.FailureHistory.Remove(item.Item);
                //remove from failure history.
            }
            else if (item.Optional == false)
            {
                //add to the failure history (or increment it).
                IncrementFailure(state, item.Item);
            }
        }
        _needsSaving = true;
        return output;
    }
    private static bool ShouldAward(double chance) => chance >= 1 || Random.Shared.NextDouble() <= chance;
    private static void IncrementFailure(WorksiteAutomationStateModel state, string item)
    {
        if (state.FailureHistory.TryGetValue(item, out int times))
        {
            state.FailureHistory[item] = times + 1;
        }
        else
        {
            state.FailureHistory[item] = 1;
        }
        state.FailureHistory[item] = Math.Min(state.FailureHistory[item], 4); //after 4, don't increase anymore no matter what.
    }


    // ------------------------------------------------------------
    // Blocking deposit logic
    // ------------------------------------------------------------

    // Deposits rewards in order; removes only successfully deposited entries.
    // Returns true if all rewards were deposited (Rewards becomes empty).
    private bool TryDepositStoredRewards_NoLock(WorksiteAutomationStateModel state)
    {
        for (int i = 0; i < state.Rewards.Count; i++)
        {
            var reward = state.Rewards[i];

            if (inventory.CanAdd(reward) == false) // <-- map to your real API
            {
                _needsSaving = true;
                return false;
            }

            inventory.Add(reward.Item, reward.Amount); // <-- map to your real API

            state.Rewards.RemoveAt(i);
            i--;

            _needsSaving = true;
        }

        return true;
    }

    // ------------------------------------------------------------
    // Inputs / duration
    // ------------------------------------------------------------

    private bool CanConsumeInputsForRun_NoLock(string location)
    {
        if (timedBoostManager.HasNoSuppliesNeededForWorksites())
        {
            return true;
        }
        var recipe = GetRecipe_NoLock(location);
        return inventory.Has(recipe.Inputs); // <-- map to your recipe shape/API
    }

    private void ConsumeInputsForRun_NoLock(string location)
    {
        if (timedBoostManager.HasNoSuppliesNeededForWorksites())
        {
            return;
        }
        var recipe = GetRecipe_NoLock(location);
        inventory.Consume(recipe.Inputs); // <-- map to your recipe shape/API
        _needsSaving = true;
    }
    public string GetDuration(string location)
    {
        var time = GetRunDuration_NoLock(location);
        return time.GetTimeString;
    }
    private TimeSpan GetRunDuration_NoLock(string location)
    {
        var recipe = GetRecipe_NoLock(location);


        double baseM = balanceManager.Base.WorksiteTimeMultiplier;


        var reduction = timedBoostManager.GetReducedTime(location);
        if (reduction < TimeSpan.Zero)
        {
            reduction = TimeSpan.Zero;
        }


        TimeSpan baseDuration = recipe.Duration;
        var reducedBase = baseDuration - reduction;
        if (reducedBase < TimeSpan.Zero)
        {
            reducedBase = TimeSpan.Zero;
        }
        return reducedBase.Apply(baseM);


        // future: workshop upgrades, pins, etc.
        //double? speedBonus = null; // future
        //bool canInstant = false;   // future

        //double bonusM = speedBonus.SpeedBonusToTimeMultiplier(canInstant);

        //double m = baseM * bonusM;

        // keep your existing min rule inside Apply (ex: 2 seconds)
        //return recipe.Duration.Apply(m, canInstant);

    }

    // ------------------------------------------------------------
    // Lookup helpers
    // ------------------------------------------------------------

    private WorksiteAutomationStateModel GetWorksite(string location) =>
        _worksites.Single(x => x.Location.Equals(location, StringComparison.OrdinalIgnoreCase));

    private WorksiteRecipe GetRecipe_NoLock(string location) =>
        _worksiteRecipes.Single(x => x.Location.Equals(location, StringComparison.OrdinalIgnoreCase));

    private int GetOutstandingRuns_NoLock(string location)
    {
        var s = GetWorksite(location);
        return Math.Max(0, s.RequestedTotal - s.DeliveredTowardRequest);
    }

    private int GetRemainingQueueSpace_NoLock(string location)
    {
        int cap = GetQueueCap(location);
        int outstanding = GetOutstandingRuns_NoLock(location);
        return Math.Max(0, cap - outstanding);
    }
    private void ReleaseWorkers(WorksiteAutomationStateModel s)
    {
        if (s.Workers.Count == 0)
        {
            return;
        }

        // You said you don't delete workers; releasing here means "not committed"
        s.Workers.Clear();
        _needsSaving = true;
    }
    private bool IsWorkerUnlockedNow(string workerName)
    {
        return _workerStates.Single(x => x.Name == workerName).Unlocked;
    }

    private void EnsureInit()
    {
        if (_init == false)
        {
            throw new CustomBasicException("WorksiteAutomationManager not initialized.");
        }
    }
    TimeSpan IWorksiteManager.GetTimeForWorksiteItem(string itemName)
    {
        foreach (var worksite in _worksites)
        {
            var recipe = GetRecipe_NoLock(worksite.Location);
            if (recipe.BaselineBenefits.Exists(x => x.Item == itemName))
            {
                return recipe.Duration;
            }
        }
        throw new CustomBasicException($"Non item found for {itemName}");
    }
    async Task IWorksiteManager.UnlockWorkerAcquiredAsync(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Worker)
        {
            throw new CustomBasicException("Only workers can be acquired");
        }
        var item = _workerStates.Single(x => x.Name == store.TargetName && x.Unlocked == false);
        item.Unlocked = true;
        await _workerRepository.SaveAsync(_workerStates);
    }
    async Task IWorksiteManager.DoubleCheckActiveWorkerRentalAsync(RentalInstanceModel rental)
    {
        if (rental.Category != EnumCatalogCategory.Worker)
        {
            throw new CustomBasicException("Only workers can possibly double check rentals");
        }
        var item = _workerStates.Single(x => x.Name == rental.TargetName);
        if (item.Unlocked)
        {
            return;
        }
        item.Unlocked = true;
        await _workerRepository.SaveAsync(_workerStates);
    }
    async Task<bool> IWorksiteManager.CanDeleteWorkerRentalAsync(RentalInstanceModel rental)
    {
        if (rental.Category != EnumCatalogCategory.Worker)
        {
            throw new CustomBasicException("Only workers can possibly delete the rental");
        }
        var item = _workerStates.Single(x => x.Name == rental.TargetName);
        item.Unlocked = false;
        await _workerRepository.SaveAsync(_workerStates);
        foreach (var worksite in _worksites)
        {
            if (worksite.StartedAt is not null
                && worksite.Workers.Exists(x => x.WorkerName == rental.TargetName))
            {
                return false;
            }
        }
        return true;
    }
    void IWorksiteManager.ApplyWorksiteProgressionUnlocksFromLevels(BasicList<CatalogOfferModel> offers, int level)
    {
        var item = offers.SingleOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }
        var instance = _worksites.Single(x => x.Location == item.TargetName);
        instance.Unlocked = true;
        _needsSaving = true;
    }
    async Task IWorksiteManager.ApplyWorkerProgressionUnlocksFromLevelsAsync(BasicList<CatalogOfferModel> offers, int level)
    {
        var item = offers.SingleOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }

        var worker = _workerStates.Single(x => x.Name == item.TargetName);
        worker.Unlocked = true;
        await _workerRepository.SaveAsync(_workerStates);
    }
    int IWorksiteManager.GetWorksiteUnlockedCount(string location)
    {
        var site = _worksites.Single(x => x.Location == location);
        if (site.Unlocked)
        {
            return 1;
        }
        return 0;
    }
    int IWorksiteManager.GetUnlockedWorkersCount(string workerName)
    {
        var worker = _workerStates.Single(x => x.Name == workerName);
        if (worker.Unlocked)
        {
            return 1;
        }
        return 0;
    }
    private void TryLockAugmentationPromise_NoLock(WorksiteAutomationStateModel s)
    {
        if (s.OutputPromise is not null)
        {
            return; // already locked
        }

        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(s.Location);
        if (key is null)
        {
            return;
        }

        s.OutputPromise = outputAugmentationManager.GetSnapshot(key);
        _needsSaving = true;
    }

    private void ClearAugmentationPromise_NoLock(WorksiteAutomationStateModel s)
    {
        if (s.OutputPromise is null)
        {
            return;
        }
        s.OutputPromise = null;
        _needsSaving = true;
    }
    private static BasicList<ItemAmount> GetWorksiteAugmentationRewards_NoLock(WorksiteAutomationStateModel s)
    {
        if (s.OutputPromise is null)
        {
            return [];
        }
        BasicList<ItemAmount> output = [];
        // Optional: also include configured extras, if any
        if (s.OutputPromise.ExtraRewards is not null && s.OutputPromise.ExtraRewards.Count > 0)
        {
            foreach (var extra in s.OutputPromise.ExtraRewards)
            {
                output.Add(new ItemAmount(extra, 1));
            }
        }

        return output;
    }
}
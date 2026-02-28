namespace FarmSim.Domain.Services.Worksites;
public class WorksiteInstance(WorksiteRecipe recipe, double currentMultiplier,
    BasicList<WorkerRecipe> allWorkers,
    BasicList<UnlockModel> workstates,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    )
{
    public const string NoneLocation = "None";
    public Dictionary<string, int> FailureHistory { get; set; } = [];
    public bool Unlocked { get; set; }
    public bool Focused { get; set; }
    public TimeSpan ReduceBy { get; set; }
    public string Location => recipe.Location;

    // Base duration from recipe (canonical)
    public TimeSpan BaseDuration => recipe.Duration;

    public DateTime? StartedAt { get; private set; }
    public EnumWorksiteState Status { get; set; } = EnumWorksiteState.None;
    public OutputAugmentationSnapshot? OutputPromise { get; private set; }
    public int MaximumWorkers => recipe.MaximumWorkers;
    public BasicList<WorkerRecipe> Workers { get; private set; } = [];
    private BasicList<ItemAmount> _rewards = [];

    // NEW: current vs run multiplier
    private readonly double _currentMultiplier = TimingRules.ValidateMultiplier(currentMultiplier);
    private double? _runMultiplier; // locked per job run; null when idle

    private TimeSpan? _runDuration;
    public bool HasRecipe(string name) => recipe.BaselineBenefits.Exists(x => x.Item == name);
    private bool _needsSaving;
    public bool NeedsSaving => _needsSaving;


    public void ApplyTimeReduction(TimeSpan reduceBy)
    {
        if (reduceBy <= TimeSpan.Zero)
        {
            return;
        }
        if (Status != EnumWorksiteState.Processing || StartedAt is null)
        {
            return; // only meaningful while actively processing
        }

        // Pretend the job started earlier -> increases elapsed -> finishes sooner
        StartedAt = StartedAt.Value - reduceBy;
        _needsSaving = true;
    }

    public TimeSpan GetPreviewDuration(TimedBoostManager timedBoostManager)
    {
        // If not idle, preview should just show the locked duration
        if (Status != EnumWorksiteState.None && _runDuration.HasValue)
        {
            return _runDuration.Value;
        }

        var reduction = timedBoostManager.GetReducedTime(Location);
        if (reduction < TimeSpan.Zero)
        {
            reduction = TimeSpan.Zero;
        }

        var reducedBase = BaseDuration - reduction;
        if (reducedBase < TimeSpan.Zero)
        {
            reducedBase = TimeSpan.Zero;
        }

        return reducedBase.Apply(_currentMultiplier);
    }

    public TimeSpan EffectiveDuration
    {
        get
        {
            // If a job is in progress (or awaiting collection), use the locked duration.
            if (Status != EnumWorksiteState.None && _runDuration.HasValue)
            {
                return _runDuration.Value;
            }

            // Idle preview: no reduction applied (because reduction only locks at StartJob).
            return BaseDuration.Apply(_currentMultiplier);
            //var m = _runMultiplier ?? _currentMultiplier;
            //return BaseDuration.Apply(m);
        }
    }

    public TimeSpan? ReadyTime
    {
        get
        {
            if (Status != EnumWorksiteState.Processing || StartedAt is null)
            {
                return null;
            }

            var elapsed = DateTime.Now - StartedAt.Value;
            var remaining = EffectiveDuration - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
    public void Load(WorksiteAutoResumeModel worksite, TimedBoostManager timedBoostManager)
    {
        Unlocked = worksite.Unlocked;
        _rewards = worksite.Rewards;
        Status = worksite.Status;
        StartedAt = worksite.StartedAt;
        Focused = worksite.Focused;
        ReduceBy = worksite.ReduceBy;
        OutputPromise = worksite.OutputPromise;
        FailureHistory = worksite.FailureHistory ?? [];
        //needs to rethink this part eventually if more fields are needed.  for now its manually done.
        _runMultiplier = worksite.RunMultiplier;

        if (Status == EnumWorksiteState.Processing)
        {
            var reducedBase = BaseDuration - ReduceBy;
            if (reducedBase < TimeSpan.Zero)
            {
                reducedBase = TimeSpan.Zero;
            }

            // reduction BEFORE multiplier
            if (_runMultiplier.HasValue)
            {
                _runDuration = reducedBase.Apply(_runMultiplier.Value);
            }
            else
            {
                _runDuration = reducedBase.Apply(_currentMultiplier);
            }

        }
        else if (Status == EnumWorksiteState.None)
        {
            // amount to subtract (not the final time)
            ReduceBy = timedBoostManager.GetReducedTime(Location); // can return null/zero if none

            var reducedBase = BaseDuration - ReduceBy;
            if (reducedBase < TimeSpan.Zero)
            {
                reducedBase = TimeSpan.Zero;
            }

            if (_runMultiplier.HasValue)
            {
                _runDuration = reducedBase.Apply(_runMultiplier.Value);
            }
            else
            {
                _runDuration = reducedBase.Apply(_currentMultiplier);
            }

        }
        Workers.Clear();
    }
    public void Reset()
    {
        _rewards.Clear();
        foreach (var item in Workers)
        {
            FreeWorker(item);
        }
        Focused = false; //no longer focused.
        Workers.Clear();
        StartedAt = null;
        Status = EnumWorksiteState.None;
        OutputPromise = null;
        // Run is over; clear promise
        _runMultiplier = null;
    }
    public void AddWorkerAfterLoading(WorkerRecipe worker)
    {
        Workers.Add(worker);
    }
    public WorksiteAutoResumeModel GetWorksiteForSaving
    {
        get
        {
            // Save promise whenever the worksite is not idle
            double? saveRun =
                Status == EnumWorksiteState.None ? null : _runMultiplier;
            FailureHistory = FailureHistory
                .Where(x => x.Value > 0)
                .ToDictionary(x => x.Key, x => x.Value);
            return new()
            {
                Name = Location,
                Rewards = _rewards,
                StartedAt = StartedAt,
                Status = Status,
                Unlocked = Unlocked,
                Workers = Workers,
                RunMultiplier = saveRun,
                Focused = Focused,
                ReduceBy = ReduceBy,
                OutputPromise = OutputPromise,
                FailureHistory = FailureHistory ?? []
            };
        }
    }
    public string StartText => recipe.StartText;
    public void AddWorker(WorkerRecipe worker)
    {
        if (Workers.Count >= MaximumWorkers)
        {
            var temp = Workers.First();
            FreeWorker(temp);
            Workers.RemoveFirstItem();
        }
        worker.WorkerStatus = EnumWorkerStatus.Selected;
        worker.CurrentLocation = recipe.Location;
        Workers.Add(worker);
    }
    private static void FreeWorker(WorkerRecipe worker)
    {
        worker.WorkerStatus = EnumWorkerStatus.None;
        worker.CurrentLocation = NoneLocation;
    }

    public void RemoveWorker(WorkerRecipe worker)
    {
        FreeWorker(worker);
        Workers.RemoveSpecificItem(worker);
    }

    public BasicList<ItemAmount> SuppliesNeeded
    {
        get
        {
            BasicList<ItemAmount> output = [];
            foreach (var item in recipe.Inputs)
            {
                output.Add(new ItemAmount(item.Key, item.Value));
            }
            return output;
        }
    }
    public bool CanStartJob(InventoryManager inventory)
    {
        if (timedBoostManager.HasNoSuppliesNeededForWorksites())
        {
            return Status == EnumWorksiteState.None; //because you don't need supplies because of the boost.
        }
        if (inventory.Has(recipe.Inputs) == false)
        {
            return false;
        }
        return Status == EnumWorksiteState.None; //you don't need workers anymore because if you have none before starting, can do automatically now.
    }

    public bool HadRewards => _rewards.Count > 0;
    public bool CanCollectRewards => Status == EnumWorksiteState.Collecting;

    public void StoreRewards(BasicList<ItemAmount> rewards)
    {
        _rewards = rewards;
    }
    public bool NeedsAutomateError()
    {
        if (Workers.Count > 0)
        {
            return false;
        }
        ;
        AssignWorkersAutomatically();
        if (Workers.Count == 0)
        {
            return true;
        }
        Workers.Clear();
        return false;
    }
    private void AssignWorkersAutomatically()
    {
        int maxs = recipe.MaximumWorkers;

        // Available pool: not working, plus allow "Selected" ONLY if it's not selected for a different location
        var pool = allWorkers
            .Where(x =>
                x.WorkerStatus == EnumWorkerStatus.None
                || (x.WorkerStatus == EnumWorkerStatus.Selected
                    && (x.CurrentLocation == NoneLocation || x.CurrentLocation == recipe.Location))
            )
            .ToBasicList();

        // Remove locked workers (not unlocked)
        workstates.ForConditionalItems(x => x.Unlocked == false, item =>
        {
            pool.RemoveAllOnly(x => x.WorkerName == item.Name);
        });

        if (pool.Count == 0)
        {
            return;
        }

        if (pool.Count < maxs)
        {
            maxs = pool.Count;
        }

        // Preferred workers list for this worksite (ordered)
        var preferred = advancedSettingsManager.Settings.GetPreferredWorkersForWorksite(Location) ?? [];

        int assignedPreferred = 0;

        // 1) If preferences exist, try assigning ONLY preferred workers (up to maxs)
        if (preferred.Count > 0)
        {
            foreach (var name in preferred)
            {
                if (Workers.Count >= maxs)
                {
                    break;
                }

                var found = pool.SingleOrDefault(x => x.WorkerName == name);
                if (found is null)
                {
                    continue; // not available: busy elsewhere, expired rental, locked, etc.
                }

                AddWorker(found);
                pool.RemoveSpecificItem(found);
                assignedPreferred++;
            }

            // If we got at least one preferred worker, STOP.
            // Do NOT fill remaining slots; keep other workers free for other worksites.
            if (assignedPreferred > 0)
            {
                return;
            }

            // Otherwise (none of the preferred were available), fall through to default auto-fill,
            // so the player still gets at least one worker if any are available.
        }

        // 2) Default behavior: fill with top-down selection (up to maxs)
        while (Workers.Count < maxs && pool.Count > 0)
        {
            AddWorker(pool.First());
            pool.RemoveFirstItem();
        }
    }
    public bool CanResetToFocused
    {
        get
        {
            if (Status != EnumWorksiteState.Processing)
            {
                return false;
            }
            if (Focused)
            {
                return false;
            }
            if (Workers.Count < recipe.MaximumWorkers)
            {
                return false; //you must have all workers there.  otherwise, too powerful.
            }


            return recipe.CanFocus;
        }
    }
    public void ResetToFocused()
    {
        if (Status != EnumWorksiteState.Processing || StartedAt is null)
        {
            throw new CustomBasicException("Job must be processing.");
        }
        if (Focused)
        {
            return; //already focused.
        }
        Focused = true;
        _runMultiplier ??= _currentMultiplier;
        _runMultiplier *= 2.0;      // Apply() uses time * multiplier, so this doubles effective time
        var reducedBase = BaseDuration - ReduceBy;
        if (reducedBase < TimeSpan.Zero)
        {
            reducedBase = TimeSpan.Zero;
        }

        _runDuration = reducedBase.Apply(_runMultiplier.Value);
        StartedAt = DateTime.Now;   // waste elapsed time (your rule)
    }
    public void StartJob(InventoryManager inventory)
    {
        _rewards.Clear();
        if (CanStartJob(inventory) == false)
        {
            throw new CustomBasicException("Unable to start job. Should have called CanStartJob first");
        }

        if (Workers.Count == 0)
        {
            AssignWorkersAutomatically();
            if (Workers.Count == 0)
            {
                return; //do this to protect me from myself.    so if no workers, then don't do anything (because no workers anyways).
            }
        }
        if (timedBoostManager.HasNoSuppliesNeededForWorksites() == false)
        {
            foreach (var item in recipe.Inputs)
            {
                inventory.Consume(item.Key, item.Value);
            }
        }
        

        Workers.ForEach(worker => worker.WorkerStatus = EnumWorkerStatus.Working);

        // LOCK promise at start
        _runMultiplier = _currentMultiplier;

        // amount to subtract (not the final time)
        ReduceBy = timedBoostManager.GetReducedTime(Location); // can return null/zero if none

        var reducedBase = BaseDuration - ReduceBy;
        if (reducedBase < TimeSpan.Zero)
        {
            reducedBase = TimeSpan.Zero;
        }

        // reduction BEFORE multiplier
        _runDuration = reducedBase.Apply(_runMultiplier.Value);
        RunPossibleAugmentation();
        StartedAt = DateTime.Now;
        Status = EnumWorksiteState.Processing;
    }

    private static bool ShouldAward(double chance) => chance >= 1 || Random.Shared.NextDouble() <= chance;

    public void CollectSpecificReward(ItemAmount amount)
    {
        if (_rewards.Count == 0)
        {
            throw new CustomBasicException("No more reward left");
        }

        _rewards.RemoveAllAndObtain(x => x.Item == amount.Item);
        if (_rewards.Count == 0)
        {
            CollectAllRewards();
        }
    }

    public ItemAmount GetFirstReward
    {
        get
        {
            if (_rewards.Count == 0)
            {
                throw new CustomBasicException("No more reward left");
            }

            ItemAmount output = _rewards.First();
            _rewards.RemoveFirstItem();

            if (_rewards.Count == 0)
            {
                CollectAllRewards();
            }
            return output;
        }
    }
    private void IncrementFailure(string item)
    {
        if (FailureHistory.TryGetValue(item, out int times))
        {
            FailureHistory[item] = times + 1;
        }
        else
        {
            FailureHistory[item] = 1;
        }
        FailureHistory[item] = Math.Min(FailureHistory[item], 4); //after 4, don't increase anymore no matter what.
    }

    private void ClearFailure(string item)
    {
        // remove completely once it succeeds
        FailureHistory.Remove(item);
    }
    public BasicList<ItemAmount> GetRewards()
    {
        if (_rewards.Count > 0)
        {
            return _rewards.ToBasicList();
        }

        if (CanCollectRewards == false)
        {
            throw new CustomBasicException("Cannot collect rewards because there was none to collect.");
        }

        BasicList<WorksiteRewardPreview> list = GetPreview(false);
        BasicList<ItemAmount> output = [];
        foreach (var item in list)
        {
            if (ShouldAward(item.Chance))
            {
                output.Add(new(item.Item, item.Amount));
                // Always clear on success (even if we weren't tracking this item yet).
                // This keeps the rule "success resets pity" true even if tracking rules change later.
                ClearFailure(item.Item);
                //remove from failure history.
            }
            else if (item.Optional == false)
            {
                //add to the failure history (or increment it).
                IncrementFailure(item.Item);
            }
        }
        return output;
    }

    public void CollectAllRewards()
    {
        Reset();
        
    }

    private void RunPossibleAugmentation()
    {
        if (OutputPromise is not null)
        {
            return; //already promised.
        }
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(Location);
        if (key is null)
        {
            return;
        }
        OutputPromise = outputAugmentationManager.GetSnapshot(key);
        _needsSaving = true;
    }

    public void UpdateTick()
    {
        _needsSaving = false;
        if (Status == EnumWorksiteState.Collecting)
        {
            RunPossibleAugmentation();
        }
        if (Status != EnumWorksiteState.Processing || StartedAt is null)
        {
            return;
        }
        RunPossibleAugmentation();
        // Defensive: ensure promise exists
        _runMultiplier ??= _currentMultiplier;

        var elapsed = DateTime.Now - StartedAt.Value;
        if (elapsed >= EffectiveDuration)
        {
            Status = EnumWorksiteState.Collecting;
            StartedAt = null;
            _needsSaving = true;
        }
    }

    // --- Your existing reward preview logic unchanged below ---

    public BasicList<WorksiteRewardPreview> GetPreview(bool uiOnly = true)
    {
        BasicList<WorksiteRewardPreview> output = [];
        WorksiteRewardPreview preview;
        if (uiOnly)
        {
            RunPossibleAugmentation();
        }
        if (Workers.Count == 0)
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

            

            if (OutputPromise is not null)
            {
                foreach (var item in OutputPromise.ExtraRewards)
                {
                    preview = new()
                    {
                        Chance = 100,
                        Amount = 1,
                        Item = item
                    };
                    output.Add(preview);
                }
            }
            if (uiOnly)
            {
                OutputPromise = null;
                RunPossibleAugmentation();
            }
            return output;
        }

        HashSet<string> candidateItems = [];

        foreach (var b in recipe.BaselineBenefits)
        {
            candidateItems.Add(b.Item);
        }
        if (Focused == false)
        {
            foreach (var worker in Workers)
            {
                foreach (var benefit in worker.Benefits)
                {
                    if (benefit.Worksite == recipe.Location)
                    {
                        candidateItems.Add(benefit.Item);
                    }
                }
            }
        }


        foreach (var review in candidateItems)
        {
            WorksiteBaselineBenefit? startBenefit = recipe.BaselineBenefits.SingleOrDefault(x => x.Item == review);
            BasicList<WorkerBenefit> workerBenefits = GetWorkerBenefits(review);
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
                    amount = startBenefit.Quantity * Workers.Count;
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

                if (FailureHistory.TryGetValue(review, out int times))
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
                    double baseChance = startBase * Workers.Count;
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
            if (Focused)
            {
                chances = 1; //make all guaranteed because you focused.
            }
            if (uiOnly)
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
        if (OutputPromise is not null)
        {
            foreach (var item in OutputPromise.ExtraRewards)
            {
                preview = new()
                {
                    Chance = 100,
                    Amount = 1,
                    Item = item
                };
                output.Add(preview);
            }
        }
        if (uiOnly)
        {
            OutputPromise = null;
            RunPossibleAugmentation();
        }
        return output;
    }

    private BasicList<WorkerBenefit> GetWorkerBenefits(string item)
    {
        if (Focused)
        {
            return [];
        }
        BasicList<WorkerBenefit> output = [];
        Workers.ForConditionalItems(x => x.CurrentLocation == recipe.Location, worker =>
        {
            var seconds = worker.Benefits.Where(x => x.Item == item);
            output.AddRange(seconds);
        });
        return output;
    }
}

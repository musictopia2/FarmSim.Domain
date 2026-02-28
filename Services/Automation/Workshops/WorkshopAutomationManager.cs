namespace FarmSim.Domain.Services.Automation.Workshops;
public class WorkshopAutomationManager(
    InventoryManager inventory,
    BalanceManager balanceManager,
    RulesManager rulesManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager
) : IWorkshopManager
{
    private bool _init;
    private IWorkshopAutomationProfile _profile = null!;
    private IWorkshopRecipes _recipesProvider = null!;
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private readonly Lock _lock = new();
    private BasicList<WorkshopRecipe> _recipes = [];
    private BasicList<WorkshopAutomationBuildingLaneStateModel> _workshops = [];
    private Dictionary<string, int> _capByItem = new(StringComparer.OrdinalIgnoreCase);
    public async Task SetStyleContextAsync(WorkshopAutomationServicesContext context, FarmKey farm)
    {
        if (_init)
        {
            throw new InvalidOperationException("Already initialized");
        }
        if (rulesManager.AutomationEnabled == false)
        {
            return; //don't do anything because no automation.  needs to double check.
        }
        _profile = context.WorkshopAutomationProfile;
        _recipesProvider = context.WorkshopRecipes;
        _recipes = await _recipesProvider.GetWorkshopRecipesAsync();
        if (_recipes.Count == 0)
        {
            throw new CustomBasicException("No workshop recipes");
        }
        // ensure registry knows workshop outputs (same as WorkshopManager)
        foreach (var r in _recipes)
        {
            itemRegistry.Register(new(r.Output.Item, EnumInventoryStorageCategory.Barn, EnumInventoryItemCategory.Workshops));
        }
        var rules = await context.WorkshopBaseRulesProvider.GetRulesAsync(farm);
        _capByItem = rules.ToDictionary(x => x.CraftedItem, x => x.StartingQueueCount, StringComparer.OrdinalIgnoreCase);
        // load automation request state
        _workshops = await _profile.LoadAsync();
        _init = true;
    }
    private int GetAugmentationExtraUnitsThisCycle(WorkshopAutomationBuildingLaneStateModel lane, WorkshopRecipe recipe)
    {
        // augmentation is keyed by OUTPUT ITEM in your manual system
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(recipe.Output.Item);
        if (key is null)
        {
            return 0;
        }

        var snap = outputAugmentationManager.GetSnapshot(key);
        if (snap.Chance <= 0)
        {
            return 0;
        }

        // One “cycle” represents lane.VirtualCount parallel crafts.
        // Mirror manual semantics: each craft can yield +1.
        int extra = 0;
        int virtualCount = GetVirtualCount(lane);
        for (int i = 0; i < virtualCount; i++)
        {
            if (rs1.RollHit(snap.Chance))
            {
                extra += 1;
            }
        }

        return extra;
    }

    private int GetMaxStoredUnitsPerCycle(WorkshopAutomationBuildingLaneStateModel lane, WorkshopRecipe recipe)
    {
        int batch = GetBatchSize(lane, recipe); // your existing batch (= VirtualCount * Output.Amount)
        return batch + GetVirtualCount(lane);
    }
    public string GetDuration(string craftedItem)
    {
        var recipe = _recipes.Single(x => x.Item == craftedItem);
        WorkshopAutomationBuildingLaneStateModel lane = GetLaneByCraftedItem(craftedItem);
        var time = GetProductionTimePerItemAdjusted(lane, recipe);
        return time.GetTimeString;
    }

    private TimeSpan GetProductionTimePerItemAdjusted(
        WorkshopAutomationBuildingLaneStateModel lane,
        WorkshopRecipe recipe
    )
    {
        if (lane is null)
        {
            throw new CustomBasicException("Still needed because future");
        }

        // base multiplier (matches CropTimeMultiplier pattern)
        double baseM = balanceManager.Base.WorkshopTimeMultiplier;
        TimeSpan reduction = timedBoostManager.GetReducedTime(recipe.BuildingName);
        if (reduction < TimeSpan.Zero)
        {
            reduction = TimeSpan.Zero;
        }
        TimeSpan duration = recipe.Duration;
        duration -= reduction;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }


        // future: workshop upgrades, pins, etc.
        double? speedBonus = advancedUpgradeAutomationManager.GetTimeReductionBenefit(recipe.BuildingName);

        double bonusM = speedBonus.SpeedBonusToTimeMultiplier(false);

        double m = baseM * bonusM;

        // keep your existing min rule inside Apply (ex: 2 seconds)
        return duration.Apply(m, false);
    }
    private WorkshopRecipe GetRecipe(string item)
    {
        return _recipes.Single(x => x.Item == item);
    }
    private int GetBatchSize(WorkshopAutomationBuildingLaneStateModel lane, WorkshopRecipe recipe)
    {
        int batch = GetVirtualCount(lane) * recipe.Output.Amount;
        if (batch <= 0)
        {
            throw new CustomBasicException($"Invalid batch size for workshop item '{recipe.Item}'.");
        }
        return batch;
    }
    public int GetVirtualCount(string buildingName)
    {
        var lane = _workshops.Single(x => x.BuildingName == buildingName);
        return GetVirtualCount(lane);
    }
    private int GetVirtualCount(WorkshopAutomationBuildingLaneStateModel workshop)
    {
        int virtualCount = workshop.VirtualCount;
        int extras = advancedUpgradeAutomationManager.ExtraVirtualCountBenefit(workshop.BuildingName);
        return virtualCount + extras;
    }
    private Dictionary<string, int> GetInputRequirements(WorkshopAutomationBuildingLaneStateModel lane, WorkshopRecipe recipe)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int virtualCount = GetVirtualCount(lane);
        foreach (var kvp in recipe.Inputs)
        {
            int need = kvp.Value * virtualCount;
            if (need > 0)
            {
                dict[kvp.Key] = need;
            }
        }
        return dict;
    }
    private bool TryDeliverStoredExactAndWasteRemainderOnComplete(
        WorkshopAutomationBuildingLaneStateModel lane,
        WorkshopAutomationItemStateModel itemState,
        WorkshopRecipe recipe,
        DateTime now,
        ref int outstanding,
        out bool completedRequest)
    {
        completedRequest = false;

        if (outstanding <= 0)
        {
            if (itemState.StoredUnits > 0)
            {
                itemState.StoredUnits = 0; // waste
            }
            completedRequest = true;
            return true;
        }

        if (itemState.StoredUnits <= 0)
        {
            return false;
        }

        int toDeliver = Math.Min(outstanding, itemState.StoredUnits);
        if (toDeliver <= 0)
        {
            return false;
        }

        if (inventory.CanAdd(recipe.Output.Item, toDeliver) == false)
        {
            // ✅ inventory full -> true block
            lane.BlockedAt ??= now;
            lane.StartedAt = now; // stop offline accrual while blocked
            return false;
        }

        inventory.Add(recipe.Output.Item, toDeliver);
        itemState.StoredUnits -= toDeliver;
        itemState.DeliveredTowardRequest += toDeliver;
        outstanding -= toDeliver;

        if (outstanding <= 0)
        {
            if (itemState.StoredUnits > 0)
            {
                itemState.StoredUnits = 0; // waste remainder
            }
            completedRequest = true;
        }

        return true;
    }
    private WorkshopAutomationBuildingLaneStateModel GetLaneByCraftedItem(string craftedItem)
    {
        foreach (var lane in _workshops)
        {
            if (lane.Items.Any(x => x.Item.Equals(craftedItem, StringComparison.OrdinalIgnoreCase)))
            {
                return lane;
            }
        }
        throw new CustomBasicException($"No workshop lane found for crafted item '{craftedItem}'.");
    }

    private WorkshopAutomationBuildingLaneStateModel GetLaneByBuildingName(string buildingName)
    {
        var lane = _workshops.SingleOrDefault(x => x.BuildingName.Equals(buildingName, StringComparison.OrdinalIgnoreCase)) ?? throw new CustomBasicException($"No workshop lane found for building '{buildingName}'.");
        return lane;
    }

    private WorkshopAutomationItemStateModel GetCraftedItem(string craftedItem)
    {
        var lane = GetLaneByCraftedItem(craftedItem);
        return lane.Items.Single(x => x.Item.Equals(craftedItem, StringComparison.OrdinalIgnoreCase));
    }

    // ----------------------------
    // Bulk helpers (automation semantics)
    // ----------------------------

    private static int CeilDiv(TimeSpan remaining, TimeSpan perUse)
    {
        // perUse should be your glove reduce amount (ex: 20 minutes).
        // If misconfigured (0), treat as "1 use completes".
        double denom = perUse.TotalMinutes;
        if (denom <= 0.000001)
        {
            return 1;
        }

        double n = remaining.TotalMinutes / denom;
        int needed = (int)Math.Ceiling(n);
        return needed < 1 ? 1 : needed;
    }

    private bool EnsureLaneHasActiveCycle_NoLock(WorkshopAutomationBuildingLaneStateModel lane, DateTime now)
    {
        // Let the lane start work if it can, or deliver stored, etc.
        // This keeps behavior consistent with your regular tick.
        ProcessBuildingLane(lane, now);

        // If it is blocked, bulk actions should stop (same idea as manual: you can't "push through" missing inputs / full barn).
        if (lane.BlockedAt is not null)
        {
            return false;
        }

        return lane.CycleEndsAt is not null && lane.ActiveItem is not null;
    }

    private void CompleteActiveJobImmediately_NoLock(WorkshopAutomationBuildingLaneStateModel lane, DateTime now)
    {
        if (lane.Unlocked == false)
        {
            return;
        }

        // Only affects a currently running cycle.
        if (lane.CycleEndsAt is null || lane.ActiveItem is null)
        {
            return;
        }

        // Force exactly one completion, then let ProcessBuildingLane start the next cycle fresh.
        lane.CycleEndsAt = now;
        lane.StartedAt = now;

        ProcessBuildingLane(lane, now);
        _needsSaving = true;
    }

    private void ApplyPowerGloveToActiveCycle_NoLock(
        WorkshopAutomationBuildingLaneStateModel lane,
        DateTime now,
        int used,
        TimeSpan reduceByPerUse
    )
    {
        if (used <= 0)
        {
            return;
        }

        if (lane.Unlocked == false)
        {
            return;
        }

        // Only affects a currently running cycle.
        if (lane.CycleEndsAt is null || lane.ActiveItem is null)
        {
            return;
        }

        var cycleEnd = lane.CycleEndsAt.Value;
        var remaining = cycleEnd - now;

        // If it already should be complete, just complete one cycle now (no carryover).
        if (remaining <= TimeSpan.Zero)
        {
            lane.CycleEndsAt = now;
            lane.StartedAt = now;
            ProcessBuildingLane(lane, now);
            _needsSaving = true;
            return;
        }

        var totalReduce = TimeSpan.FromTicks(reduceByPerUse.Ticks * used);

        // Case 1: reduce but do not complete the current cycle
        if (totalReduce < remaining)
        {
            var newRemaining = remaining - totalReduce;
            lane.CycleEndsAt = now.Add(newRemaining);

            // Keep StartedAt consistent for progress UI
            if (lane.StartedAt is not null)
            {
                lane.StartedAt = lane.StartedAt.Value - totalReduce;
            }

            _needsSaving = true;
            return;
        }

        // Case 2: finish current cycle -> COMPLETE EXACTLY ONE cycle
        // Any excess reduction is wasted (no carryover to the next cycle).
        lane.CycleEndsAt = now;
        lane.StartedAt = now;

        ProcessBuildingLane(lane, now);
        _needsSaving = true;
    }

    // ----------------------------
    // Public bulk APIs you call from UI
    // ----------------------------

    public void CompleteWorkshopJobsBulkUsingFinishSingle(WorkshopAutomationView summary)
    {
        var lane = GetLaneByBuildingName(summary.BuildingName);

        int total = inventory.Get(CurrencyKeys.FinishSingleWorkshop);
        if (total <= 0)
        {
            return;
        }

        int used = 0;

        lock (_lock)
        {
            while (used < total)
            {
                var now = DateTime.Now;

                // If we cannot run (blocked / nothing active / nothing to do), stop.
                if (EnsureLaneHasActiveCycle_NoLock(lane, now) == false)
                {
                    break;
                }

                // Complete exactly one cycle per consumable
                CompleteActiveJobImmediately_NoLock(lane, now);

                used++;
            }

            if (used > 0)
            {
                inventory.Consume(CurrencyKeys.FinishSingleWorkshop, used);
                _needsSaving = true;
            }
        }
    }

    public void CompleteWorkshopJobsBulkUsingPowerGloves(WorkshopAutomationView summary)
    {
        var lane = GetLaneByBuildingName(summary.BuildingName);

        int totalGloves = inventory.Get(CurrencyKeys.PowerGloveWorkshop);
        if (totalGloves <= 0)
        {
            return;
        }

        int glovesUsed = 0;
        TimeSpan reduceByPerUse = PowerGloveRegistry.ReduceBy; // should be 20 minutes in your design

        lock (_lock)
        {
            while (glovesUsed < totalGloves)
            {
                var now = DateTime.Now;

                // Make sure the lane is actually running something (or can start something).
                if (EnsureLaneHasActiveCycle_NoLock(lane, now) == false)
                {
                    break;
                }

                // Re-check after ensuring active
                if (lane.CycleEndsAt is null)
                {
                    break;
                }

                var remaining = lane.CycleEndsAt.Value - now;

                // If already complete, let ProcessBuildingLane resolve it without spending gloves.
                if (remaining <= TimeSpan.Zero)
                {
                    ProcessBuildingLane(lane, now);
                    continue;
                }

                // IMPORTANT: gloves needed are computed PER JOB (cycle) and any extra time is wasted.
                // This matches the manual behavior you described.
                int needed = CeilDiv(remaining, reduceByPerUse);

                // If we can't fully complete THIS cycle, stop (no partial usage).
                if (glovesUsed + needed > totalGloves)
                {
                    break;
                }

                // Apply exactly 'needed' gloves -> finishes exactly ONE cycle (with possible waste).
                ApplyPowerGloveToActiveCycle_NoLock(lane, now, needed, reduceByPerUse);

                glovesUsed += needed;
            }

            if (glovesUsed > 0)
            {
                inventory.Consume(CurrencyKeys.PowerGloveWorkshop, glovesUsed);
                _needsSaving = true;
            }
        }
    }

    public void CompleteAllJobsImmediately()
    {
        if (inventory.Has(CurrencyKeys.FinishAllWorkshops, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish all workshop consumables left.");
        }
        foreach (var item in _workshops)
        {
            CompleteActiveJobImmediately(item);
        }
        inventory.Consume(CurrencyKeys.FinishAllWorkshops, 1);
    }

    public void CompleteSingleActiveJobImmediately(WorkshopAutomationView workshop)
    {
        if (inventory.Has(CurrencyKeys.FinishSingleWorkshop, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish single workshop consumables left.  Should had called inventory.Has function");
        }
        CompleteActiveJobImmediately(workshop);
        inventory.Consume(CurrencyKeys.FinishSingleWorkshop, 1);
    }
    private void CompleteActiveJobImmediately(WorkshopAutomationBuildingLaneStateModel lane)
    {
        lock (_lock)
        {


            if (lane.Unlocked == false)
            {
                return;
            }

            // Only affects a currently running cycle.
            if (lane.CycleEndsAt is null || lane.ActiveItem is null)
            {
                return;
            }
            var now = DateTime.Now;


            lane.CycleEndsAt = now;
            lane.StartedAt = now;
            ProcessBuildingLane(lane, now);
            _needsSaving = true;


        }
    }
    private void CompleteActiveJobImmediately(WorkshopAutomationView summary)
    
    {
        var lane = _workshops.Single(x => x.BuildingName.Equals(summary.BuildingName, StringComparison.OrdinalIgnoreCase));
        CompleteActiveJobImmediately(lane);
        
    }
    public void UsePowerGlove(WorkshopAutomationView workshop, int howMany)
    {
        if (inventory.Has(CurrencyKeys.PowerGloveWorkshop, howMany) == false)
        {
            throw new CustomBasicException("Don't have enough power gloves.  Should had called the inventorymanager.Has function");
        }
        ApplyPowerGloveToActiveJob(workshop, howMany, PowerGloveRegistry.ReduceBy);
        inventory.Consume(CurrencyKeys.PowerGloveWorkshop, howMany);
    }
    private void ApplyPowerGloveToActiveJob(WorkshopAutomationView summary, int used, TimeSpan reduceByPerUse)
    {
        if (used <= 0)
        {
            return;
        }

        var now = DateTime.Now;
        var totalReduce = TimeSpan.FromTicks(reduceByPerUse.Ticks * used);

        lock (_lock)
        {
            var lane = _workshops.Single(x => x.BuildingName.Equals(summary.BuildingName, StringComparison.OrdinalIgnoreCase));

            if (lane.Unlocked == false)
            {
                return;
            }

            // Only affects a currently running cycle.
            if (lane.CycleEndsAt is null || lane.ActiveItem is null)
            {
                return;
            }

            var cycleEnd = lane.CycleEndsAt.Value;

            // If it already should be complete, just let the next tick handle it (or force one completion now)
            var remaining = cycleEnd - now;
            if (remaining <= TimeSpan.Zero)
            {
                // Optional: force exactly one completion right now.
                lane.CycleEndsAt = now;
                lane.StartedAt = now;
                ProcessBuildingLane(lane, now);
                _needsSaving = true;
                return;
            }

            // Case 1: glove(s) reduce time but do NOT finish the current cycle
            if (totalReduce < remaining)
            {
                var newRemaining = remaining - totalReduce;
                lane.CycleEndsAt = now.Add(newRemaining);

                // Keep StartedAt consistent for progress UI if you use StartedAt/CycleEndsAt
                // started = now - elapsed; elapsed = perCycle - remaining
                // So: started' = started - totalReduce
                if (lane.StartedAt is not null)
                {
                    lane.StartedAt = lane.StartedAt.Value - totalReduce;
                }

                _needsSaving = true;
                return;
            }

            // Case 2: glove(s) finish the current cycle -> COMPLETE EXACTLY ONE cycle
            // Any excess reduction is wasted (no carryover).
            lane.CycleEndsAt = now;

            // Important: reset timing anchor so the next cycle starts fresh (full duration).
            // ProcessBuildingLane will run the while-loop ONCE and then set CycleEndsAt = now + perCycle.
            lane.StartedAt = now;

            ProcessBuildingLane(lane, now);

            _needsSaving = true;
        }
    }
    public WorkshopAutomationView? SearchForWorkshop(string searchFor)
    {
        // Find the recipe that produces the desired item
        WorkshopRecipe? target = _recipes.FirstOrDefault(x => x.Item == searchFor); //you may have more than one.   if more than one, has to choose the first one.  you are on your own from here.
        if (target is null)
        {
            return null;
        }

        // Find the workshop instance that owns that recipe
        WorkshopAutomationBuildingLaneStateModel t = _workshops.First(x => x.BuildingName == target.BuildingName); //has to be first now because you can have more than one workshop with the same name.


        return new WorkshopAutomationView
        {
            BuildingName= t.BuildingName,
            SelectedItem = searchFor
        };
    }
    public int GetCap(string item)
    {
        if (_capByItem.TryGetValue(item, out int cap) == false)
        {
            throw new CustomBasicException($"Must have a cap for {item}");
        }
        var lane = GetLaneByCraftedItem(item);
        int totalCap = cap;
        int extras = advancedUpgradeAutomationManager.ExtraQueCountBenefit(item);
        totalCap += extras;
        return totalCap * GetVirtualCount(lane);
    }

    public int GetOutstanding(string item)
    {
        var s = GetCraftedItem(item);
        return s.RequestedTotal - s.DeliveredTowardRequest;
    }

    public int GetRemainingCapacity(string item)
    {
        int cap = GetCap(item);
        int outstanding = GetOutstanding(item);
        int remaining = cap - outstanding;
        return remaining < 0 ? 0 : remaining;
    }
    public void Request(string item, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            return;
        }

        EnsureRecipeExists(item);

        int cap = GetCap(item);
        if (cap <= 0)
        {
            return;
        }

        int remaining = GetRemainingCapacity(item);
        int add = Math.Min(amountToAdd, remaining);
        if (add <= 0)
        {
            return;
        }

        var crafted = GetCraftedItem(item);
        var lane = GetLaneByBuildingName(crafted.BuildingName);

        bool buildingWasInactive = GetOutstandingForBuilding(crafted.BuildingName) <= 0;

        crafted.RequestedTotal += add;

        if (buildingWasInactive)
        {
            // kick the lane awake (like animals)
            lane.ActiveItem = null;   // let picker choose the best craftable
            lane.BlockedAt = null;
            lane.StartedAt = null;

            // clear stored for this item (same as animals)
            crafted.StoredUnits = 0;
        }

        _needsSaving = true;
    }

    // ----------------------------
    // Tick
    // ----------------------------

    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }

        DateTime now = DateTime.Now;

        foreach (var lane in _workshops)
        {
            ProcessBuildingLane(lane, now);
        }

        await SaveAsync();
    }
    private int GetOutstandingForBuilding(string buildingName)
    {
        var lane = GetLaneByBuildingName(buildingName);
        int total = 0;
        foreach (var s in lane.Items)
        {
            if (s.Unlocked == false)
            {
                continue;
            }
            total += (s.RequestedTotal - s.DeliveredTowardRequest);
        }
        return total;
    }


    private bool CanStartItemNow(WorkshopAutomationBuildingLaneStateModel lane, WorkshopRecipe recipe)
    {
        // If recipe is for a locked item, skip
        var itemState = lane.Items.SingleOrDefault(x => x.Item.Equals(recipe.Item, StringComparison.OrdinalIgnoreCase));
        if (itemState is null || itemState.Unlocked == false)
        {
            return false;
        }

        int outstanding = itemState.RequestedTotal - itemState.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            return false;
        }

        // If we already have stored units, the only thing we care about is: can we deliver at least 1?
        if (itemState.StoredUnits > 0)
        {
            return inventory.CanAdd(recipe.Output.Item, 1);
        }

        // Otherwise need inputs to produce
        var reqs = GetInputRequirements(lane, recipe);
        if (reqs.Count > 0 && inventory.Has(reqs) == false)
        {
            return false;
        }

        // producing into StoredUnits is allowed even if barn is full (delivery will block later)
        return true;
    }

    private string? PickNextCraftableItemForLane(WorkshopAutomationBuildingLaneStateModel lane)
    {
        foreach (var r in _recipes.Where(x => x.BuildingName == lane.BuildingName))
        {
            if (CanStartItemNow(lane, r))
            {
                return r.Item;
            }
        }
        return null;
    }

    private void ProcessBuildingLane(WorkshopAutomationBuildingLaneStateModel lane, DateTime now)
    {
        if (lane.Unlocked == false)
        {
            return;
        }

        int GetOutstandingForState(WorkshopAutomationItemStateModel s)
            => s.RequestedTotal - s.DeliveredTowardRequest;

        int GetOutstandingForBuilding()
        {
            int total = 0;
            foreach (var s in lane.Items)
            {
                if (s.Unlocked == false)
                {
                    continue;
                }

                total += GetOutstandingForState(s);
            }
            return total;
        }

        // ------------------------------------------------------------
        // 1) BLOCKED MODE (ONLY when BlockedAt != null)
        //    - deliver stored if possible
        //    - if nothing stored, re-check craftability and unblock
        // ------------------------------------------------------------
        if (lane.BlockedAt is not null)
        {
            lane.CycleEndsAt = null; // blocked lanes do not run cycles

            // Try deliver any stored that can fit
            var deliverable = lane.Items
                .Where(x => x.Unlocked && x.StoredUnits > 0)
                .Select(x => new { State = x, Recipe = GetRecipe(x.Item) })
                .FirstOrDefault(x => inventory.CanAdd(x.Recipe.Output.Item, 1));

            if (deliverable is not null)
            {
                var st = deliverable.State;
                var rec = deliverable.Recipe;

                int outstanding = GetOutstandingForState(st);
                TryDeliverStoredExactAndWasteRemainderOnComplete(lane, st, rec, now, ref outstanding, out bool completed);

                if (completed)
                {
                    lane.ActiveItem = null;
                    lane.StartedAt = null;
                    lane.BlockedAt = null;
                    lane.CycleEndsAt = null;
                    st.StoredUnits = 0;
                }

                _needsSaving = true;
                return;
            }

            // No stored to deliver -> maybe we were blocked due to missing inputs.
            var craftable = PickNextCraftableItemForLane(lane);
            if (craftable is null)
            {
                // still blocked
                return;
            }

            // unblock and continue this tick
            lane.BlockedAt = null;
            lane.ActiveItem = craftable;
            lane.StartedAt = now;
            lane.CycleEndsAt = null;
            _needsSaving = true;
            // fall through
        }

        // ------------------------------------------------------------
        // 2) RUNNING CYCLE MODE (offline catch-up lives here)
        //    IMPORTANT: If CycleEndsAt != null, DO NOT pick, DO NOT block for missing inputs.
        // ------------------------------------------------------------
        if (lane.CycleEndsAt is not null)
        {
            if (lane.ActiveItem is null)
            {
                // inconsistent state; safest is cancel the cycle
                lane.CycleEndsAt = null;
                _needsSaving = true;
                return;
            }

            var itemState = lane.Items.Single(x => x.Item.Equals(lane.ActiveItem, StringComparison.OrdinalIgnoreCase));
            var recipe = GetRecipe(lane.ActiveItem);

            // If request already satisfied, stop.
            if (GetOutstandingForState(itemState) <= 0)
            {
                lane.ActiveItem = null;
                lane.StartedAt = null;
                lane.CycleEndsAt = null;
                _needsSaving = true;
                return;
            }

            TimeSpan perCycle = GetProductionTimePerItemAdjusted(lane, recipe);
            int batch = GetBatchSize(lane, recipe);
            var requirements = GetInputRequirements(lane, recipe);

            DateTime cycleEnd = lane.CycleEndsAt.Value;

            // If not done yet, just wait
            if (now < cycleEnd)
            {
                _needsSaving = true;
                return;
            }

            // Catch up as many completed cycles as allowed
            while (now >= cycleEnd)
            {
                // Produce into storage (cap at one cycle worth, INCLUDING possible augmentation)
                int extra = GetAugmentationExtraUnitsThisCycle(lane, recipe);
                int produced = batch + extra;

                itemState.StoredUnits += produced;

                int maxStored = GetMaxStoredUnitsPerCycle(lane, recipe);
                if (itemState.StoredUnits > maxStored)
                {
                    itemState.StoredUnits = maxStored;
                }

                // Attempt delivery
                int outstanding = GetOutstandingForState(itemState);
                TryDeliverStoredExactAndWasteRemainderOnComplete(lane, itemState, recipe, now, ref outstanding, out bool completed);

                if (completed)
                {
                    lane.ActiveItem = null;
                    lane.StartedAt = null;
                    lane.CycleEndsAt = null;
                    lane.BlockedAt = null;
                    itemState.StoredUnits = 0;
                    _needsSaving = true;
                    return;
                }

                if (lane.BlockedAt is not null)
                {
                    // Barn full -> true block: stop running
                    lane.CycleEndsAt = null;
                    lane.StartedAt = now; // freeze accrual
                    _needsSaving = true;
                    return;
                }

                // If we still have stored units, stop producing more until they get delivered.
                if (itemState.StoredUnits > 0)
                {
                    lane.CycleEndsAt = null;
                    lane.ActiveItem = null;   // let lane focus on delivery next time
                    lane.StartedAt = null;
                    _needsSaving = true;
                    return;
                }

                // Start NEXT cycle (consumes inputs NOW; offline approximation)
                if (requirements.Count > 0)
                {
                    if (inventory.Has(requirements) == false)
                    {
                        // Missing inputs -> block
                        lane.BlockedAt ??= now;
                        lane.CycleEndsAt = null;
                        lane.ActiveItem = null;
                        lane.StartedAt = now;
                        _needsSaving = true;
                        return;
                    }
                    inventory.Consume(requirements);
                }

                cycleEnd = cycleEnd.Add(perCycle);
            }

            // Persist next cycle end
            lane.CycleEndsAt = cycleEnd;
            _needsSaving = true;
            return;
        }

        // ------------------------------------------------------------
        // 3) IDLE MODE (no cycle running): pick or block or start
        // ------------------------------------------------------------

        // pick next craftable (includes "deliver stored if any" logic via CanStartItemNow)
        string? nextItem = PickNextCraftableItemForLane(lane);

        if (nextItem is null)
        {
            if (GetOutstandingForBuilding() > 0)
            {
                // Missing inputs
                lane.BlockedAt ??= now;
                lane.ActiveItem = null;
                lane.StartedAt = now; // freeze accrual
                lane.CycleEndsAt = null;
                _needsSaving = true;
                return;
            }

            // Truly idle
            lane.ActiveItem = null;
            lane.StartedAt = null;
            lane.CycleEndsAt = null;
            lane.BlockedAt = null;
            _needsSaving = true;
            return;
        }

        // Focus the chosen item
        lane.ActiveItem = nextItem;
        lane.StartedAt ??= now;

        var state = lane.Items.Single(x => x.Item.Equals(nextItem, StringComparison.OrdinalIgnoreCase));
        var rec0 = GetRecipe(nextItem);

        int out0 = GetOutstandingForState(state);
        if (out0 <= 0)
        {
            lane.ActiveItem = null;
            lane.StartedAt = null;
            _needsSaving = true;
            return;
        }

        // If we have stored units, try deliver immediately (no cycle start)
        if (state.StoredUnits > 0)
        {
            TryDeliverStoredExactAndWasteRemainderOnComplete(lane, state, rec0, now, ref out0, out bool completed0);
            if (completed0)
            {
                lane.ActiveItem = null;
                lane.StartedAt = null;
                lane.CycleEndsAt = null;
                lane.BlockedAt = null;
                state.StoredUnits = 0;
            }
            _needsSaving = true;
            return;
        }

        // Start a cycle by consuming NOW
        TimeSpan per = GetProductionTimePerItemAdjusted(lane, rec0);
        var reqs0 = GetInputRequirements(lane, rec0);

        if (reqs0.Count > 0 && inventory.Has(reqs0) == false)
        {
            // missing inputs -> block
            lane.BlockedAt ??= now;
            lane.ActiveItem = null;
            lane.StartedAt = now;
            lane.CycleEndsAt = null;
            _needsSaving = true;
            return;
        }

        if (reqs0.Count > 0)
        {
            inventory.Consume(reqs0);
        }

        lane.CycleEndsAt = now.Add(per);
        _needsSaving = true;
    }
    private void EnsureRecipeExists(string item)
    {
        if (_recipes.Any(x => x.Item == item))
        {
            return;
        }

        throw new CustomBasicException($"Workshop recipe not found for '{item}'.");
    }

    private async Task SaveAsync()
    {
        if (_needsSaving == false)
        {
            return;
        }

        // throttle (same idea as your other managers)
        if ((DateTime.Now - _lastSave).TotalSeconds < 1)
        {
            return;
        }

        _lastSave = DateTime.Now;
        _needsSaving = false;

        await _profile.SaveAsync(_workshops);

        // If you want to persist lane state too, add it to the profile later.
        // For day-1 simplicity, lane state can be “ephemeral” (it rebuilds deterministically next tick).
    }
    WorkshopRecipeQuestModel IWorkshopManager.GetWorkshopForQuests(string itemCrafting)
    {
        if (_init == false)
        {
            throw new CustomBasicException("Did not even initialize");
        }
        if (_recipes.Count == 0)
        {
            throw new CustomBasicException("No workshop recipes");
        }
        var recipe = _recipes.Single(x => x.Item == itemCrafting);
        return new()
        {
            BuildingName = recipe.BuildingName,
            Duration = recipe.Duration,
            Inputs = new Dictionary<string, int>(recipe.Inputs),
            Item = recipe.Item,
        };
    }

    bool IWorkshopManager.IsInBuilding(string buildingName, string itemToCheck)
    {
        var recipe = _recipes.Single(x => x.Item == itemToCheck);
        return recipe.BuildingName == buildingName;
    }

    void IWorkshopManager.ApplyWorkshopProgressionOnLevelUnlocks(BasicList<ItemUnlockRule> rules, BasicList<CatalogOfferModel> offers, int level)
    {
        //only unlock current level.
        var modify = rules.Where(x => x.LevelRequired == level);
        bool changed = false;
        foreach (var craftedItem in modify)
        {
            WorkshopRecipe recipe = _recipes.Single(x => x.Item == craftedItem.ItemName);
            var list = _workshops.Where(x => x.BuildingName == recipe.BuildingName);
            foreach (var item in list)
            {
                var fins = item.Items.Single(x => x.Item == craftedItem.ItemName);
                fins.Unlocked = true;
                changed = true;
            }
        }
        var offer = offers.FirstOrDefault(x => x.LevelRequired == level);
        if (offer is not null)
        {
            var workshop = _workshops.First(x => x.BuildingName == offer.TargetName);
            workshop.Unlocked = true;
            changed = true;

        }
        if (changed)
        {
            _needsSaving = true;
        }
    }

    int IWorkshopManager.GetUnlockedCount(string buildingName)
    {
        return _workshops.Count(x => x.BuildingName == buildingName && x.Unlocked);
    }

    void IWorkshopManager.PurchaseWorkshop(StoreItemRowModel store)
    {
        throw new CustomBasicException("Cannot purchase workshops when automation is used");
    }
    public BasicList<WorkshopAutomationView> GetUnlockedWorkshops
    {
        get
        {
            BasicList<WorkshopAutomationView> output = [];

            _workshops.ForConditionalItems(x => x.Unlocked, t =>
            {
                WorkshopAutomationView summary = new()
                {
                    BuildingName = t.BuildingName,
                    StartedAt = t.StartedAt
                };
                output.Add(summary);
            });


            return output;
        }
    }
    public BasicList<string> GetUnlockedCraftedItemsForBuilding(string buildingName)
    {
        var lane = GetLaneByBuildingName(buildingName);

        // Keep stable ordering using recipes (instead of lane.Items)
        var ordered = _recipes
            .Where(x => x.BuildingName.Equals(buildingName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Item)
            .ToBasicList();

        // Filter to only unlocked in this lane
        var unlockedSet = lane.Items
            .Where(x => x.Unlocked)
            .Select(x => x.Item)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new BasicList<string>();
        foreach (var item in ordered)
        {
            if (unlockedSet.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    public bool IsBlocked(string craftedItem)
    {
        var lane = GetLaneByCraftedItem(craftedItem);
        return lane.BlockedAt is not null;
    }

    public TimeSpan? GetTimeUntilNextReady(string craftedItem)
    {
        if (_init == false)
        {
            return null;
        }

        var lane = GetLaneByCraftedItem(craftedItem);
        if (lane.Unlocked == false)
        {
            return null;
        }

        // If blocked, UI should show "Blocked" not a timer
        if (lane.BlockedAt is not null)
        {
            return null;
        }

        var state = GetCraftedItem(craftedItem);
        int outstanding = state.RequestedTotal - state.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            return null;
        }

        // If we already have stored units, next delivery could be immediate (if barn has room)
        if (state.StoredUnits > 0)
        {
            var recipe0 = GetRecipe(craftedItem);
            return inventory.CanAdd(recipe0.Output.Item, 1) ? TimeSpan.Zero : null;
        }

        // If not actively crafting this item, we can't give a meaningful countdown
        if (string.Equals(lane.ActiveItem, craftedItem, StringComparison.OrdinalIgnoreCase) == false)
        {
            return null;
        }

        if (lane.StartedAt is null)
        {
            return null;
        }

        var recipe = GetRecipe(craftedItem);
        TimeSpan perCycle = GetProductionTimePerItemAdjusted(lane, recipe);

        var elapsed = DateTime.Now - lane.StartedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return perCycle;
        }

        double per = Math.Max(0.001, perCycle.TotalSeconds);
        double into = elapsed.TotalSeconds % per;
        double remain = per - into;

        if (remain < 0.05)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(remain);
    }
    public Dictionary<string, int> GetFullRequirements(string craftedItem)
    {
        if (_init == false)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var lane = GetLaneByCraftedItem(craftedItem);
        var recipe = GetRecipe(craftedItem);

        // IMPORTANT: this matches the automation “start” rule (virtual count scaling)
        return GetInputRequirements(lane, recipe);
    }
}
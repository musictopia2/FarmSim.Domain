namespace FarmSim.Domain.Services.Automation.Trees;
public class TreeAutomationManager(InventoryManager inventory,
    BalanceManager balanceManager,
    RulesManager rulesManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager
    ) : ITreeManager
{
    private bool _init;
    private ITreeAutomationProfile _profileService = null!;
    private ITreeBaseRulesProvider _baseRulesProvider = null!;
    private readonly Lock _lock = new();
    private BasicList<TreeAutomationStateModel> _trees = [];
    private BasicList<TreeRecipe> _recipes = [];
    private bool _needsSaving;
    private ITreesCollecting _treeCollecting = null!;
    private DateTime _lastSave = DateTime.MinValue;
    private Dictionary<string, int> _capByTree = new(StringComparer.OrdinalIgnoreCase);
    public TimeSpan? GetTimeUntilNextReady(string treeName)
    {
        if (_init == false)
        {
            return null;
        }

        var tree = _trees.Single(x => x.TreeName == treeName);

        if (tree.IsSuppressed || tree.Unlocked == false)
        {
            return null;
        }

        int outstanding = tree.RequestedTotal - tree.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            return null; // no active request
        }

        // If we already have produced items waiting, "next" is ready now.
        if (tree.StoredUnits > 0)
        {
            return TimeSpan.Zero;
        }

        // If blocked, the real reason is "can't store", so time doesn't matter.
        // UI should show stuck instead of a timer.
        if (tree.BlockedAt is not null)
        {
            return null;
        }

        if (tree.StartedAt is null)
        {
            return null;
        }

        var recipe = _recipes.Single(x => x.TreeName == tree.TreeName);
        var perItem = GetProductionTimePerItemAdjusted(recipe);
        if (perItem <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        TimeSpan elapsed = DateTime.Now - tree.StartedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return perItem;
        }

        // Since you don't store partials, this is the only correct estimate:
        // remaining = perItem - (elapsed % perItem)
        double modSeconds = elapsed.TotalSeconds % perItem.TotalSeconds;
        double remainingSeconds = perItem.TotalSeconds - modSeconds;

        // Edge case: if we're extremely close, clamp to 0.
        if (remainingSeconds < 0.001)
        {
            remainingSeconds = 0;
        }

        return TimeSpan.FromSeconds(remainingSeconds);
    }
    public bool IsSuppressed(string treeName) => _trees.Single(x => x.TreeName == treeName).IsSuppressed;
    public BasicList<GrantableItem> GetUnlockedTreeGrantItems()
    {
        CustomBasicException.ThrowIfNull(_treeCollecting);

        int amount = _treeCollecting.TreesCollectedAtTime;

        // Distinct by TreeName (or Item) to guarantee no duplicates
        var unlockedTreeNames = _trees
            .Where(t => t.Unlocked)
            .Select(t => t.TreeName)
            .Distinct();

        BasicList<GrantableItem> output = [];

        foreach (var name in unlockedTreeNames)
        {
            TreeRecipe recipe = _recipes.Single(r => r.TreeName == name);

            output.Add(new GrantableItem
            {
                Item = recipe.Item,
                Amount = amount,
                Category = EnumItemCategory.Tree,
                Source = recipe.Item
            });
        }
        return output;
    }
    public BasicList<TreeAutomationView> GetUnlockedTrees
    {
        get
        {
            BasicList<TreeAutomationView> output = [];
            _trees.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, t =>
            {
                TreeRecipe recipe = _recipes.Single(x => x.TreeName == t.TreeName);
                TreeAutomationView summary = new()
                {
                    ItemName = recipe.Item,
                    TreeName = t.TreeName,
                    StartedAt = t.StartedAt,
                };
                output.Add(summary);
            });
            return output;
        }
    }
    public bool IsBlocked(TreeAutomationView tree)
    {
        return _trees.Single(x => x.TreeName == tree.TreeName).BlockedAt is not null;
    }
    public bool HasTrees(string name) => _recipes.Exists(x => x.Item == name);
    public async Task SetStyleContextAsync(TreeAutomationServicesContext context, FarmKey farm)
    {
        if (rulesManager.AutomationEnabled == false)
        {
            return; //don't do anything because no automation.  needs to double check.
        }
        _profileService = context.Profile;
        _baseRulesProvider = context.TreeBaseRulesProvider;
        _treeCollecting = context.TreesCollecting;
        _recipes = await context.TreeRecipes.GetTreesAsync();
        foreach (var item in _recipes)
        {
            itemRegistry.Register(new(item.Item, EnumInventoryStorageCategory.Silo, EnumInventoryItemCategory.Trees));
        }
        var rules = await _baseRulesProvider.GetRulesAsync(farm);
        _capByTree = rules.ToDictionary(x => x.TreeName, x => x.StartingQueueCount, StringComparer.OrdinalIgnoreCase);
        _trees = await _profileService.LoadAsync(); //must already have it.
        _init = true;
    }
    public void GrantUnlimitedTreeItems(GrantableItem item)
    {
        if (item.Category != EnumItemCategory.Tree)
        {
            throw new CustomBasicException("This is not a tree");
        }

        if (inventory.CanAdd(item) == false)
        {
            throw new CustomBasicException("Unable to add because was full.  Should had ran the required functions first");
        }
        //since this is unlimited, then no need for extra items (since you get literally what you ask for).
        //AddTreeToInventory(item.Item, item.Amount);
        inventory.Add(item.Item, item.Amount); //hopefully this simple.
    }
    public void SetTreeSuppressionByProducedItem(string itemName, bool supressed)
    {
        var recipe = _recipes.Single(x => x.Item == itemName);

        var tree = _trees.Single(x => x.TreeName == recipe.TreeName);
        tree.IsSuppressed = supressed;
        _needsSaving = true;
    }
    public void ApplyTreeUnlocksOnLevels(BasicList<CatalogOfferModel> offers, int level) //actually since this is from leveling, has to apply t
    {
        //only unlock current level.
        var item = offers.FirstOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }
        var instance = _trees.Single(x => x.TreeName == item.TargetName);
        instance.Unlocked = true;
        _needsSaving = true;
    }
    private bool CanGrantTreeItems(GrantableItem item, int toUse)
    {
        if (toUse <= 0)
        {
            return false;
        }
        if (item.Category != EnumItemCategory.Tree)
        {
            return false;
        }
        if (inventory.Get(CurrencyKeys.SpeedSeed) < toUse)
        {
            return false;
        }
        int amount;
        amount = item.Amount;
        bool maxed;
        maxed = false;
        //maxed = _trees.Any(x => x.MaxBenefits);
        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        if (maxed)
        {
            amount *= 2;
        }
        int granted = toUse * amount;

        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Item); //i think.
        if (temp is null)
        {
            return inventory.CanAdd(item.Item, granted);
        }
        if (inventory.CanAdd(item.Item, granted + 1) == false)
        {
            return false;
        }
        return true;
    }
    public void GrantTreeItems(GrantableItem item, int toUse)
    {
        if (CanGrantTreeItems(item, toUse) == false)
        {
            throw new CustomBasicException("Unable to grant tree items.  Should had used CanGrantTreeItems first");
        }

        bool maxed = false;

        //maxed = _trees.Any(x => x.MaxBenefits);
        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        int perSeed = item.Amount;
        if (maxed)
        {
            perSeed *= 2;
        }

        int granted = toUse * perSeed;

        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Item);
        if (temp is null)
        {
            AddTreeToInventory(item.Item, granted);
            inventory.Consume(CurrencyKeys.SpeedSeed, toUse);
            return;
        }
        var fins = outputAugmentationManager.GetSnapshot(temp);
        if (fins.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Must have one reward");
        }
        if (fins.Chance >= 100)
        {
            throw new CustomBasicException("Cannot be a guarantee for crops");
        }
        if (fins.ExtraRewards.Single() != item.Item)
        {
            throw new CustomBasicException("The extra reward does not match the item being granted");
        }
        bool hit = rs1.RollHit(fins.Chance);
        if (hit)
        {
            AddExtraReward(fins.ExtraRewards.Single(), 1);
        }
        AddTreeToInventory(item.Item, granted);
        inventory.Consume(CurrencyKeys.SpeedSeed, toUse);
    }
    private void AddExtraReward(string item, int amount)
    {
        if (amount == 0)
        {
            return;
        }
        //OnAugmentedOutput?.Invoke(new ItemAmount(item, amount));
        inventory.Add(item, amount);
    }
    private void AddTreeToInventory(string name, int amount)
    {
        //this is used so if i ever have the ability of getting something else in future, will be here.
        inventory.Add(name, amount);
        _needsSaving = true;
    }
    public void Request(string treeName, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            return;
        }
        EnsureRecipeExists(treeName);
        int cap = GetCap(treeName);
        if (cap <= 0)
        {
            return; // v1: cannot request if no queue cap authored
        }
        var s = _trees.Single(x => x.TreeName == treeName);
        int remainingCapacity = GetRemainingCapacity(treeName);
        int add = Math.Min(amountToAdd, remainingCapacity);
        if (add <= 0)
        {
            return;
        }

        bool wasInactive = (s.RequestedTotal - s.DeliveredTowardRequest) <= 0;

        s.RequestedTotal += add;

        // Only start “time tracking” when it becomes active
        if (wasInactive)
        {
            s.StartedAt = DateTime.Now;
            s.StoredUnits = 0;
        }
        _needsSaving = true;
    }
    public int GetCap(string treeName)
    {
        if (_capByTree.TryGetValue(treeName, out int cap))
        {
            var recipe = _recipes.Single(x => x.TreeName == treeName);
            //var item = _trees.Single(x => x.TreeName == treeName);
            int additional = advancedUpgradeAutomationManager.ExtraQueCountBenefit(recipe.Item);
            int totals = cap + additional;
            var tree = _trees.Single(x => x.TreeName == treeName);
            return totals * GetVirtualCount(recipe, tree.VirtualCount);
        }
        throw new CustomBasicException($"Must have a cap for {treeName}");
    }
    public int GetOutstanding(string treeName)
    {
        var s = _trees.Single(x => x.TreeName == treeName);
        return s.RequestedTotal - s.DeliveredTowardRequest;
    }

    public int GetRemainingCapacity(string treeName)
    {
        int cap = GetCap(treeName);
        int outstanding = GetOutstanding(treeName);
        int remaining = cap - outstanding;
        return remaining < 0 ? 0 : remaining;
    }


    private void EnsureRecipeExists(string treeName)
    {
        if (_recipes.Any(x => x.TreeName == treeName))
        {
            return;
        }
        throw new CustomBasicException($"Tree recipe not found for '{treeName}'.");
    }
    private void ApplyAugmentationForHarvests(TreeAutomationStateModel tree, TreeRecipe recipe, int harvests)
    {
        if (harvests <= 0)
        {
            return;
        }
        // For trees, augmentation key should be by produced item (recipe.Item)
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(recipe.Item);
        if (key is null)
        {
            if (tree.StoredExtraUnits > 0)
            {
                _needsSaving = true;
            }
            tree.StoredExtraUnits = 0;
            return;
        }

        var snap = outputAugmentationManager.GetSnapshot(key);

        if (snap.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Tree augmentation must have exactly one reward");
        }

        if (!string.Equals(snap.ExtraRewards.Single(), recipe.Item, StringComparison.OrdinalIgnoreCase))
        {
            throw new CustomBasicException("Tree augmentation extra reward must match produced item");
        }

        if (snap.IsDouble)
        {
            throw new CustomBasicException("Trees use same-item extras, not doubles"); // your choice
        }

        if (snap.Chance <= 0)
        {
            return;
        }

        int rolls = harvests * GetVirtualCount(tree);   // ✅ scale with “how many trees”
        int hits = 0;
        for (int i = 0; i < rolls; i++)
        {
            if (rs1.RollHit(snap.Chance))
            {
                hits++;
            }
        }

        if (hits > 0)
        {
            tree.StoredExtraUnits += hits;
            _needsSaving = true;
        }
    }
    private void UpdateTick(TreeAutomationStateModel tree, DateTime dateUsed)
    {
        if (tree.StartedAt is null)
        {
            return;
        }
        if (tree.IsSuppressed)
        {
            return;
        }
        int outstanding = tree.RequestedTotal - tree.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            tree.StartedAt = null;
            tree.StoredExtraUnits = 0;
            _needsSaving = true;
            return;
        }

        EnsureRecipeExists(tree.TreeName);
        var recipe = _recipes.Single(x => x.TreeName == tree.TreeName);
        int virtualCount = GetVirtualCount(tree);
        // --------------- A) BLOCKED MODE (player fault => no catch-up) ---------------
        if (tree.BlockedAt is not null)
        {
            bool deliveredAny = TryDeliverStored(tree, recipe, ref outstanding);

            if (outstanding <= 0)
            {
                tree.StartedAt = null;
                tree.BlockedAt = null;
                tree.StoredUnits = 0;
                tree.StoredExtraUnits = 0;
                _needsSaving = true;
                return;
            }

            // Still blocked if at least 1 unit is ready and we can't store 1.

            bool stillBlocked = tree.StoredUnits >= virtualCount && inventory.CanAdd(recipe.Item, virtualCount) == false;
            if (stillBlocked)
            {
                if (deliveredAny)
                {
                    _needsSaving = true;
                }

                // IMPORTANT: do NOT change StartedAt while blocked.
                // Time passing while blocked is wasted (won't generate output).
                return;
            }

            // Unblocked now: clear blocked + reset production anchor so blocked duration never becomes output.
            tree.BlockedAt = null;
            tree.StartedAt = dateUsed;

            if (deliveredAny)
            {
                _needsSaving = true;
            }

            // Do not also accumulate production this tick after unblocking.
            return;
        }

        // --------------- B) NORMAL MODE (offline catch-up allowed) ---------------
        // ---------- NORMAL MODE: catch-up allowed ----------
        TimeSpan elapsed = dateUsed - tree.StartedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var perItem = GetProductionTimePerItemAdjusted(recipe);
        if (perItem <= TimeSpan.Zero)
        {
            return;
        }

        int harvests = (int)Math.Floor(elapsed.TotalSeconds / perItem.TotalSeconds);
        if (harvests <= 0)
        {
            return;
        }
        ApplyAugmentationForHarvests(tree, recipe, harvests);
        // advance anchor by the consumed full harvest time (keeps remainder)
        tree.StartedAt = tree.StartedAt.Value.AddSeconds(harvests * perItem.TotalSeconds);

        // convert harvests -> units
        int producedUnits = harvests * virtualCount;
        tree.StoredUnits += producedUnits;

        // deliver what you can; if you hit full inventory, block
        bool delivered = TryDeliverStored(tree, recipe, ref outstanding);
        if (outstanding <= 0)
        {
            tree.StartedAt = null;
            tree.BlockedAt = null;
            tree.StoredUnits = 0;
            tree.StoredExtraUnits = 0;
            _needsSaving = true;
            return;
        }

        if (tree.StoredUnits > 0 && inventory.CanAdd(recipe.Item, virtualCount) == false)
        {
            tree.BlockedAt = dateUsed;
        }
        if (delivered || producedUnits > 0)
        {
            _needsSaving = true;
        }
    }
    private bool TryDeliverStored(TreeAutomationStateModel tree, TreeRecipe recipe, ref int outstanding)
    {
        bool deliveredAny = false;
        int batch = GetVirtualCount(tree);

        while (outstanding > 0)
        {
            // total available to deliver (base + extra same-item)
            int available = tree.StoredUnits + tree.StoredExtraUnits;
            if (available <= 0)
            {
                break;
            }

            int deliver = Math.Min(available, outstanding);
            if (deliver <= 0)
            {
                break;
            }

            if (inventory.CanAdd(recipe.Item, deliver) == false)
            {
                break;
            }

            // deliver all at once
            inventory.Add(recipe.Item, deliver);
            tree.DeliveredTowardRequest += deliver;
            outstanding -= deliver;
            deliveredAny = true;

            // consume from extras first (they are "free singles"), then from base
            int useExtra = Math.Min(tree.StoredExtraUnits, deliver);
            tree.StoredExtraUnits -= useExtra;
            int remaining = deliver - useExtra;

            if (remaining > 0)
            {
                // now consume remaining from base; base is produced in batches
                // consume as many full batches as needed, wasting remainder within the last batch (your existing rule)
                int batchesNeeded = (int)Math.Ceiling(remaining / (double)batch);
                tree.StoredUnits -= batchesNeeded * batch;
                if (tree.StoredUnits < 0)
                {
                    tree.StoredUnits = 0; // safety clamp
                }
            }
        }
        return deliveredAny;
    }
    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }

        DateTime now = DateTime.Now;
        _trees.ForConditionalItems(x => x.IsSuppressed == false && x.Unlocked == true, tree =>
        {
            UpdateTick(tree, now);
        });

        await SaveTreesAsync();
    }
    private async Task SaveTreesAsync()
    {
        bool doSave = false;
        lock (_lock)
        {
            if (_needsSaving && DateTime.Now - _lastSave > TimingRules.SaveThrottle)
            {
                _needsSaving = false;
                doSave = true;
                _lastSave = DateTime.Now;
            }
        }
        if (doSave)
        {
            await _profileService.SaveAsync(_trees);
        }
    }
    public string GetDuration(TreeAutomationView view)
    {
        var recipe = _recipes.Single(x => x.TreeName == view.TreeName);
        var time = GetProductionTimePerItemAdjusted(recipe);
        return time.GetTimeString;
    }
    private TimeSpan GetProductionTimePerItemAdjusted(TreeRecipe recipe)
    {
        double baseM = balanceManager.Base.TreeTimeMultiplier;

        double? speedBonus = advancedUpgradeAutomationManager.GetTimeReductionBenefit(recipe.Item);
        double bonusM = speedBonus.SpeedBonusToTimeMultiplier(false);

        double m = baseM * bonusM;

        // 1) Figure out how many items come out of one "harvest/cycle"
        int yieldPerHarvest = Math.Max(1, _treeCollecting.TreesCollectedAtTime); // rename to whatever your recipe uses

        // 2) Compute the stated/harvest time (what manual players perceive)
        // If you intentionally set ProductionTimeForEach so that yield*each = stated, this works:
        TimeSpan harvestTime = TimeSpan.FromTicks(recipe.ProductionTimeForEach.Ticks * yieldPerHarvest);

        // 3) Apply the pin reduction at the HARVEST level
        var reduction = timedBoostManager.GetReducedTime(recipe.Item);
        if (reduction < TimeSpan.Zero)
        {
            reduction = TimeSpan.Zero;
        }

        harvestTime -= reduction;
        if (harvestTime < TimeSpan.Zero)
        {
            harvestTime = TimeSpan.Zero;
        }

        // 4) Convert back to per-item for automation “as it goes along”
        TimeSpan perItem = TimeSpan.FromTicks(harvestTime.Ticks / yieldPerHarvest);

        // 5) Apply global multipliers/min rules once at the end
        return perItem.Apply(m, false);
    }
    //public TimeSpan GetTimeForGivenTree(string name) => 
    void ITreeManager.PurchaseTree(StoreItemRowModel store)
        => throw new CustomBasicException("Automated trees cannot be purchased in automation mode.");

    int ITreeManager.GetUnlockedCount(string treeName)
    {
        int count = _trees.Count(x => x.TreeName == treeName && x.Unlocked && x.IsSuppressed == false);
        if (count == 1)
        {
            return 1;
        }
        if (count > 1)
        {
            throw new CustomBasicException($"Should not have {count} of {treeName}");
        }
        return 0;
    }
    TimeSpan ITreeManager.GetTimeForGivenTree(string treeName) => _recipes.Single(x => x.Item == treeName).ProductionTimeForEach;
    public int GetVirtualCount(string treeName)
    {
        var tree = _trees.Single(x => x.TreeName == treeName);
        return GetVirtualCount(tree);
    }
    private int GetVirtualCount(TreeAutomationStateModel tree)
    {
        int count = tree.VirtualCount;
        TreeRecipe recipe = _recipes.Single(x => x.TreeName == tree.TreeName);
        return GetVirtualCount(recipe, count);
    }
    private int GetVirtualCount(TreeRecipe recipe, int count)
    {
        int additionals = advancedUpgradeAutomationManager.ExtraVirtualCountBenefit(recipe.Item);
        return count + additionals;
    }
}
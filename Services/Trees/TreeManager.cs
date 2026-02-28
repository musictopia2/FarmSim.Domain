namespace FarmSim.Domain.Services.Trees;
public class TreeManager(InventoryManager inventory,
    BalanceManager balanceManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    ) : ITreeManager, IScenarioResettable
{
    private ITreesCollecting? _treeCollecting;
    private ITreeRepository _treeRepository = null!;
    private BasicList<TreeRecipe> _recipes = [];
    private readonly Lock _lock = new();
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private readonly BasicList<TreeInstance> _trees = [];
    private double _offset;
    public event Action<ItemAmount>? OnAugmentedOutput;
    private BasicList<TreeUpgradeTierRule> _upgradeRules = [];

    // Public read-only summaries for the UI
    public int GetLevel(TreeView tree)
    {
        var instance = GetTreeById(tree);
        return instance.Level;
    }
    public bool IsFast(TreeView tree) => _recipes.Single(x => x.TreeName == tree.TreeName).IsFast;

    public int LevelRequiredForUpgrade(TreeView tree, int levelDesired)
    {
        var upgrade = _upgradeRules.Single(x => x.TreeName == tree.TreeName);
        return upgrade.TierLevels[levelDesired - 2];
    }
    public string GetAdjustedTimeForGivenTree(TreeView tree)
    {
        CustomBasicException.ThrowIfNull(_treeCollecting);

        TreeRecipe recipe = _recipes.Single(x => x.Item == tree.ItemName);
        TreeInstance instance = GetTreeById(tree);

        int batchSize = _treeCollecting.TreesCollectedAtTime;

        // Timed-boost reduced-by applies to the next batch (same as your crop preview).
        TimeSpan reducedBy = timedBoostManager.GetReducedTime(tree.ItemName);

        bool canInstant = instance.MaxBenefits && recipe.IsFast;

        double speedBonusMultiplier = instance.AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);

        // Same composition rule as crops:
        // final multiplier = profile offset * speed-bonus-as-time-multiplier
        double m = _offset * speedBonusMultiplier;

        // This must match TreeInstance's ProductionTimePerTreeAdjusted logic:
        // per-tree adjusted time (already accounts for reducedBy + min-total rules).
        TimeSpan perTreeAdjusted =
            recipe.ProductionTimeForEach.ApplyWithMinTotalForBatch(m, batchSize, reducedBy, canInstant);

        // TreeInstance treats the harvest as "batchSize trees produced" => total batch time.
        TimeSpan totalBatch = TimeSpan.FromTicks(perTreeAdjusted.Ticks * batchSize);

        return totalBatch.GetTimeString;
    }
    public void UpgradeTreeLevel(TreeView tree, double extraSpeed, bool maxBenefits)
    {
        var instance = GetTreeById(tree);
        instance.Level++;
        instance.AdvancedSpeedBonus = extraSpeed;
        instance.MaxBenefits = maxBenefits;
        _needsSaving = true;
    }
    public BasicList<TreeView> GetUnlockedTrees
    {
        get
        {
            BasicList<TreeView> output = [];
            _trees.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, t =>
            {
                TreeView summary = new()
                {
                    Id = t.Id,
                    ItemName = t.Name,
                    TreeName = t.TreeName,
                    IsRental = t.IsRental
                };
                output.Add(summary);
            });
            return output;
        }
    }
    public TimeSpan GetTimeForGivenTree(string name) => _recipes.Single(x => x.Item == name).ProductionTimeForEach;
   
    public bool CanDeleteRental(Guid id)
    {
        TreeInstance tree = _trees.Single(x => x.Id == id);
        if (tree.Unlocked == false)
        {
            return true;
        }
        if (tree.IsRental == false)
        {
            tree.IsRental = true; //implies its a rental.
            _needsSaving = true;
        }
        if (tree.RentalExpired == false)
        {
            tree.RentalExpired = true;
            _needsSaving = true;
            return false;
        }
        return false;
    }

    public void DoubleCheckActiveRental(Guid id)
    {
        TreeInstance tree = _trees.Single(x => x.Id == id);
        if (tree.IsRental == false)
        {
            tree.IsRental = true;
            _needsSaving = true;
        }
        if (tree.Unlocked == false)
        {
            tree.Unlocked = true;
            _needsSaving = true;
        }
        if (tree.RentalExpired)
        {
            tree.RentalExpired = false;
            _needsSaving = true;
        }
    }
    public Guid StartRental(StoreItemRowModel rental)
    {
        //will have to run some tests.
        if (rental.Category != EnumCatalogCategory.Tree)
        {
            throw new CustomBasicException("Only trees can be rented");
        }
        var instance = _trees.LastOrDefault(x => x.TreeName == rental.TargetName && x.Unlocked == false) ?? throw new CustomBasicException("No locked tree available to rent");
        instance.Unlocked = true;
        instance.RentalExpired = false; //because you started the rental now.
        instance.IsRental = true; //so later can lock the proper one.  also ui can show the details for it as well.
        _needsSaving = true;
        return instance.Id;
    }

    public void UnlockTreePaidFor(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Tree)
        {
            throw new CustomBasicException("Only trees can be paid for");
        }
        var instance = _trees.First(x => x.TreeName == store.TargetName && x.Unlocked == false && x.IsRental == false);
        instance.Unlocked = true;
        _needsSaving = true;
    }
    public void SetTreeSuppressionByProducedItem(string itemName, bool supressed)
    {
        _trees.ForConditionalItems(x => x.Name == itemName, item =>
        {
            item.IsSuppressed = supressed;
        });
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
        var instance = _trees.First(x => x.TreeName == item.TargetName && x.Unlocked == false);
        instance.Unlocked = true;
        _needsSaving = true;

    }
    // Private helper to find tree by Id
    public int GetProduceAmount(TreeView tree)
    {
        CustomBasicException.ThrowIfNull(tree); //still needs to pass since i may use in future.
        CustomBasicException.ThrowIfNull(_treeCollecting);
        return _treeCollecting.TreesCollectedAtTime; // for now
    }
    private TreeInstance GetTreeById(Guid id)
    {
        var tree = _trees.SingleOrDefault(t => t.Id == id) ?? throw new CustomBasicException($"Tree with Id {id} not found.");
        return tree;
    }
    private TreeInstance GetTreeById(TreeView id) => GetTreeById(id.Id);

    public bool HasTrees(string name) => _recipes.Exists(x => x.Item == name);
    public TimeSpan TreeDuration(TreeView id) => GetTreeById(id).BaseTime;

    // Methods for UI to query dynamic state
    public int TreesReady(TreeView id) => GetTreeById(id).TreesReady;
    public string GetTreeName(TreeView id) => GetTreeById(id).Name;

    public string TimeLeftForResult(TreeView id)
    {
        var item = GetTreeById(id);
        if (item.ReadyTime is null)
        {
            return "";
        }
        return item.ReadyTime.Value.GetTimeString;
    }

    //public string TimeLeftForResult(TreeView id) => GetTreeById(id).ReadyTime?.Value!.GetTimeString;
    public EnumTreeState GetTreeState(TreeView id) => GetTreeById(id).State;
    //this is when you collect only one item.

    public BasicList<GrantableItem> GetUnlockedTreeGrantItems()
    {
        CustomBasicException.ThrowIfNull(_treeCollecting);

        int amount = _treeCollecting.TreesCollectedAtTime;

        // Distinct by TreeName (or Item) to guarantee no duplicates
        var unlockedTreeNames = _trees
            .Where(t => t.Unlocked)
            .Select(t => t.Name)
            .Distinct();

        BasicList<GrantableItem> output = [];

        foreach (var name in unlockedTreeNames)
        {
            //TreeRecipe recipe = _recipes.Single(r => r.TreeName == treeName);

            output.Add(new GrantableItem
            {
                Item = name,
                Amount = amount,
                Category = EnumItemCategory.Tree,
                Source = name
            });
        }
        return output;
    }
    public bool CanCollectFromTree(TreeView id)
    {
        TreeInstance instance = GetTreeById(id);
        int amount = GetCollectAmount(instance);

        if (instance.MaxBenefits)
        {
            amount *= 2;
        }

        if (instance.OutputPromise is not null)
        {
            return inventory.CanAdd(instance.Name, amount * 2);
        }
        return inventory.CanAdd(instance.Name, amount);
    }
    private int GetCollectAmount(TreeInstance instance)
    {
        //int maxs;
        if (advancedSettingsManager.Settings.CollectAllAvailableFromTrees)
        {
            
            return instance.TreesReady;
            
        }
        
        return 1;
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
        AddTreeToInventory(item.Item, item.Amount);

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

        maxed = _trees.Any(x => x.MaxBenefits);
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

        int amount;
        amount = item.Amount;
        bool maxed;

        maxed = _trees.Any(x => x.MaxBenefits);
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
    public void CollectFromTree(TreeView id)
    {
        if (CanCollectFromTree(id) == false)
        {
            throw new CustomBasicException("Unable to collect from tree.  Should had used CanCollectFromTree");
        }
        TreeInstance instance = GetTreeById(id);
        int maxs = GetCollectAmount(instance);
        TimeSpan reducedBy = timedBoostManager.GetReducedTime(instance.Name);
        var temps = instance.OutputPromise; //must store this first.
        int extras = 0;
        maxs.Times(x =>
        {
            if (temps is not null)
            {
                if (rs1.RollHit(temps.Chance))
                {
                    extras++;
                }
            }
            instance.CollectTree(reducedBy);
        });
        AddExtraReward(instance.Name, extras);
        if (instance.MaxBenefits)
        {
            maxs *= 2;
        }
        AddTreeToInventory(instance.Name, maxs);
    }
    private void AddExtraReward(string item, int amount)
    {
        if (amount == 0)
        {
            return;
        }
        OnAugmentedOutput?.Invoke(new ItemAmount(item, amount));
        inventory.Add(item, amount);
    }
    private void AddTreeToInventory(string name, int amount)
    {
        //this is used so if i ever have the ability of getting something else in future, will be here.
        inventory.Add(name, amount);
        _needsSaving = true;
    }

    public async Task SetStyleContextAsync(TreeServicesContext context, FarmKey farm)
    {
        if (_treeRepository != null)
        {
            throw new InvalidOperationException("Repository Already set");
        }
        _treeRepository = context.TreeRepository;
        _recipes = await context.TreeRecipes.GetTreesAsync();
        _upgradeRules = await context.UpgradeTierRules.GetRulesAsync(farm);
        foreach (var item in _recipes)
        {
            itemRegistry.Register(new(item.Item, EnumInventoryStorageCategory.Silo, EnumInventoryItemCategory.Trees));
        }
        var ours = await context.TreeRepository.LoadAsync();
        _trees.Clear();
        _treeCollecting = context.TreesCollecting;
        _offset = balanceManager.Base.TreeTimeMultiplier;
        foreach (var item in ours)
        {
            if (item.State == EnumTreeState.Collecting && item.TreesReady == 0)
            {
                item.TreesReady = _treeCollecting.TreesCollectedAtTime;
                _needsSaving = true; //this is one part that broke when moving to new system.
            }
            TreeRecipe recipe = _recipes.Single(x => x.TreeName == item.TreeName);
            TreeInstance tree = new(recipe, _treeCollecting, _offset, timedBoostManager, outputAugmentationManager);
            tree.Load(item);
            _trees.Add(tree);
        }
    }
    // Tick method called by game timer
    public async Task UpdateTickAsync()
    {
        _trees.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, tree =>
        {
            tree.UpdateTick();
            if (tree.NeedsToSave)
            {
                _needsSaving = true;
            }
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
            BasicList<TreeAutoResumeModel> list = _trees
             .Select(tree => tree.GetTreeForSaving)
             .ToBasicList();
            await _treeRepository.SaveAsync(list);
        }
    }

    void ITreeManager.PurchaseTree(StoreItemRowModel store)
    {
        UnlockTreePaidFor(store);
    }

    int ITreeManager.GetUnlockedCount(string treeName)
    {
        return _trees.Count(x => x.TreeName == treeName && x.Unlocked && x.IsSuppressed == false);
    }

    void IScenarioResettable.ResetForScenario()
    {
        lock (_lock)
        {
            foreach (var tree in _trees)
            {
                tree.Reset();
            }
            _needsSaving = true;
        }
    }
}
namespace FarmSim.Domain.Services.Automation.Crops;
public class CropAutomationManager(InventoryManager inventory,
    BalanceManager balanceManager,
    RulesManager rulesManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager
    ) : ICropManager
{
    private bool _init;
    private ICropAutomationProfile _profileService = null!;
    //private ICropBaseRulesProvider _baseRulesProvider = null!;
    private readonly Lock _lock = new();
    private BasicList<CropAutomationStateModel> _crops = [];
    private BasicList<CropRecipe> _recipes = [];
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private Dictionary<string, int> _capByCrop = new(StringComparer.OrdinalIgnoreCase);
    public BasicList<GrantableItem> GetUnlockedCropGrantItems()
    {
        BasicList<GrantableItem> output = [];
        _crops.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, temp =>
        {
            var recipe = _recipes.Single(x => x.Item == temp.Name);
            output.Add(new()
            {
                Amount = recipe.HowMany,
                Category = EnumItemCategory.Crop,
                Item = temp.Name,
                Source = temp.Name
            });
        });
        return output;
    }
    public bool IsSuppressed(string cropName) => _crops.Single(x => x.Name == cropName).IsSuppressed;
    public TimeSpan? GetTimeUntilNextReady(string cropName)
    {
        if (_init == false)
        {
            return null;
        }

        var crop = _crops.Single(x => x.Name == cropName);

        if (crop.IsSuppressed || crop.Unlocked == false)
        {
            return null;
        }

        int outstanding = crop.RequestedTotal - crop.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            return null; // no active request
        }

        // If we already have produced items waiting, next is ready now.
        if (crop.StoredUnits > 0)
        {
            return TimeSpan.Zero;
        }

        // If blocked, the real reason is "can't store a batch", so time doesn't matter.
        if (crop.BlockedAt is not null)
        {
            return null;
        }

        if (crop.StartedAt is null)
        {
            return null;
        }

        var recipe = _recipes.Single(x => x.Item == crop.Name);
        var perCycle = GetProductionTimePerItemAdjusted(crop, recipe);
        if (perCycle <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        TimeSpan elapsed = DateTime.Now - crop.StartedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return perCycle;
        }

        // remaining = perCycle - (elapsed % perCycle)
        double modSeconds = elapsed.TotalSeconds % perCycle.TotalSeconds;
        double remainingSeconds = perCycle.TotalSeconds - modSeconds;

        if (remainingSeconds < 0.001)
        {
            remainingSeconds = 0;
        }

        return TimeSpan.FromSeconds(remainingSeconds);
    }

    public BasicList<CropAutomationView> GetUnlockedCrops
    {
        get
        {
            BasicList<CropAutomationView> output = [];
            _crops.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, t =>
            {
                CropRecipe recipe = _recipes.Single(x => x.Item == t.Name);
                CropAutomationView summary = new()
                {
                    CropName = t.Name,
                    StartedAt = t.StartedAt,
                };
                output.Add(summary);
            });
            return output;
        }
    }
    public bool IsBlocked(CropAutomationView crop)
    {
        return _crops.Single(x => x.Name == crop.CropName).BlockedAt is not null;
    }
    public bool HasCrops(string name) => _recipes.Exists(x => x.Item == name);
    public async Task SetStyleContextAsync(CropAutomationServicesContext context, FarmKey farm)
    {
        if (rulesManager.AutomationEnabled == false)
        {
            return; //don't do anything because no automation.  needs to double check.
        }
        _profileService = context.CropAutomationProfile;
        _recipes = await context.CropRecipes.GetCropsAsync();
        if (_recipes.Count == 0)
        {
            throw new CustomBasicException("No Crop Recipes");
        }
        foreach (var item in _recipes)
        {
            itemRegistry.Register(new(item.Item, EnumInventoryStorageCategory.Silo, EnumInventoryItemCategory.Crops));
        }
        var rules = await context.CropBaseRulesProvider.GetRulesAsync(farm);
        _capByCrop = rules.ToDictionary(x => x.CropName, x => x.StartingQueueCount, StringComparer.OrdinalIgnoreCase);
        _crops = await _profileService.LoadAsync(); //must already have it.
        _init = true;
    }
    public bool CanGrantCropItems(GrantableItem item, int toUse)
    {
        if (toUse <= 0)
        {
            return false;
        }
        if (item.Category != EnumItemCategory.Crop)
        {
            return false;
        }
        if (inventory.Get(CurrencyKeys.SpeedSeed) < toUse)
        {
            return false;
        }
        bool maxed;
        maxed = false;
        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        int amount = item.Amount;
        if (maxed)
        {
            amount++;
        }
        int granted = toUse * amount;

        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Item); //i think.
        if (temp is null)
        {
            return inventory.CanAdd(item.Item, granted);
        }
        var fins = outputAugmentationManager.GetSnapshot(temp);
        BasicList<ItemAmount> bundles = [];
        bundles.Add(new(item.Item, granted));
        if (fins.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Must have one reward");
        }
        bundles.Add(new(fins.ExtraRewards.Single(), 1)); //will assume you will receive even if no guarantees.
        return inventory.CanAcceptRewards(bundles);
    }
    public void GrantCropItems(GrantableItem item, int toUse)
    {
        if (CanGrantCropItems(item, toUse) == false)
        {
            throw new CustomBasicException("Cannot grant crop items.  Should had called the CanGrantCropItems first");
        }
        bool maxed;
        maxed = false;
        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        int amount = item.Amount;
        if (maxed)
        {
            amount++;
        }

        int granted = toUse * amount;
        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Item);
        if (temp is not null)
        {
            var fins = outputAugmentationManager.GetSnapshot(temp);
            if (fins.ExtraRewards.Count != 1)
            {
                throw new CustomBasicException("Must have one reward");
            }
            if (fins.Chance >= 100)
            {
                throw new CustomBasicException("Cannot be a guarantee for crops");
            }
            bool hit = rs1.RollHit(fins.Chance);
            if (hit)
            {
                AddExtraRewards(fins.ExtraRewards.Single(), 1);
            }
        }
        AddCrop(item.Item, granted);
        inventory.Consume(CurrencyKeys.SpeedSeed, toUse);
    }
    private void AddExtraRewards(string item, int amount)
    {
        ItemAmount payLoad = new()
        {
            Amount = amount,
            Item = item
        };
        //OnAugmentedOutput?.Invoke(payLoad);
        inventory.Add(payLoad);
    }
    private void AddCrop(string item, int amount)
    {
        inventory.Add(item, amount);
        _needsSaving = true;
    }
    public void GrantUnlimitedCropItems(GrantableItem item)
    {
        if (item.Category != EnumItemCategory.Crop)
        {
            throw new CustomBasicException("This is not a crop");
        }
        if (inventory.CanAdd(item.Item, item.Amount) == false)
        {
            throw new CustomBasicException("Unable to add because was full.  Should had ran the required functions first");
        }
        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Item); //i think.
        if (temp is null)
        {
            AddCrop(item.Item, item.Amount);
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
        int count = rs1.ComputeUnlimitedBonus(item.Amount, fins.Chance);
        if (count > 10)
        {
            count = 10; //the most you can receive from it is 10 to prevent abuse.
        }
        if (count > 0)
        {
            AddExtraRewards(fins.ExtraRewards.Single(), count);
        }
        AddCrop(item.Item, item.Amount);
    }
    public void SetCropSuppressionByProducedItem(string itemName, bool supressed)
    {
        var recipe = _recipes.Single(x => x.Item == itemName);
        var crop = _crops.Single(x => x.Name == itemName);
        crop.IsSuppressed = supressed;
        _needsSaving = true;
    }
    public void ApplyCropUnlocksOnLevels(BasicList<CatalogOfferModel> offers, int level) //actually since this is from leveling, has to apply t
    {
        //only unlock current level.
        var item = offers.FirstOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }
        var instance = _crops.Single(x => x.Name == item.TargetName);
        instance.Unlocked = true;
        _needsSaving = true;
    }
    private OutputAugmentationSnapshot? GetCurrentAugSnapshotForCrop(string cropName)
    {
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(cropName);
        return key is null ? null : outputAugmentationManager.GetSnapshot(key);
    }
    
    public void Request(string cropName, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            return;
        }
        EnsureRecipeExists(cropName);
        int cap = GetCap(cropName);
        if (cap <= 0)
        {
            return; // v1: cannot request if no queue cap authored
        }
        var s = _crops.Single(x => x.Name == cropName);
        int remainingCapacity = GetRemainingCapacity(cropName);
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
            s.StoredUnits = 0; // reset request-run augmentation state
            s.StoredExtraUnits = 0;
        }
        _needsSaving = true;
    }

    public int GetVirtualCount(string cropName)
    {
        var item = _crops.Single(x => x.Name == cropName);
        return GetVirtualCount(item);
    }
    private int GetVirtualCount(CropAutomationStateModel crop)
    {
        int count = crop.VirtualCount;
        int additionals = advancedUpgradeAutomationManager.ExtraVirtualCountBenefit(crop.Name);
        return count + additionals;
    }
    public int GetCap(string cropName)
    {
        if (_capByCrop.TryGetValue(cropName, out int cap))
        {
            var item = _crops.Single(x => x.Name == cropName);
            int additional = advancedUpgradeAutomationManager.ExtraQueCountBenefit(cropName);
            int totals = cap + additional;
            return totals * GetVirtualCount(item);
        }
        throw new CustomBasicException($"Must have a cap for {cropName}");
    }
    public int GetOutstanding(string cropName)
    {
        var s = _crops.Single(x => x.Name == cropName);
        return s.RequestedTotal - s.DeliveredTowardRequest;
    }
    public int GetRemainingCapacity(string cropName)
    {
        int cap = GetCap(cropName);
        int outstanding = GetOutstanding(cropName);
        int remaining = cap - outstanding;
        return remaining < 0 ? 0 : remaining;
    }
    private void EnsureRecipeExists(string cropName)
    {
        if (_recipes.Any(x => x.Item == cropName))
        {
            return;
        }
        throw new CustomBasicException($"Crop recipe not found for '{cropName}'.");
    }
    private int GetBatchSize(CropAutomationStateModel crop, CropRecipe recipe)
    {
        // How many ITEMS are produced per completed "cycle" across all producers.
        // VirtualCount = producer count (2 free, 3 for shrimp/crabs, etc.)
        // HowMany = recipe yield per producer per cycle
        int batch = GetVirtualCount(crop) * recipe.HowMany;
        if (batch <= 0)
        {
            // If this happens, your authored data is invalid.
            throw new CustomBasicException($"Invalid batch size for crop '{crop.Name}'. VirtualCount={crop.VirtualCount}, HowMany={recipe.HowMany}");
        }
        return batch;
    }

    private bool CanStoreOneBatch(CropAutomationStateModel crop, CropRecipe recipe)
    {
        int batchSize = GetBatchSize(crop, recipe);
        return inventory.CanAdd(recipe.Item, batchSize);
    }
    private void ApplyAugmentationForCycles(CropAutomationStateModel crop, int producedCycles)
    {
        if (producedCycles <= 0)
        {
            return;
        }

        var snap = GetCurrentAugSnapshotForCrop(crop.Name);
        if (snap is null)
        {
            return;
        }

        if (snap.IsDouble)
        {
            throw new CustomBasicException("Crops cannot support doubles");
        }

        if (snap.Chance >= 100)
        {
            throw new CustomBasicException("Crops cannot be guaranteed extras");
        }

        if (snap.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Crops must have exactly one extra reward");
        }

        int rolls = producedCycles * GetVirtualCount(crop); // ✅ scale by “how many crops you have”
        if (rolls <= 0)
        {
            return;
        }

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
            // IMPORTANT: don't wipe stored extras on a miss; let them accumulate until delivered
            crop.StoredExtraUnits += hits;
            crop.StoredExtraReward ??= snap.ExtraRewards.Single();

            // If you want to support switching reward item mid-run, you need a dictionary (see below)
            if (!string.Equals(crop.StoredExtraReward, snap.ExtraRewards.Single(), StringComparison.OrdinalIgnoreCase))
            {
                // simplest safe rule: don't mix; just keep old stored until delivered, and ignore new for now
                // OR switch to a dictionary.
            }

            _needsSaving = true;
        }
    }
    private void UpdateTick(CropAutomationStateModel crop, DateTime dateUsed)
    {
        if (crop.IsSuppressed)
        {
            return;
        }

        int outstanding = crop.RequestedTotal - crop.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            // No active request => ensure we are idle and nothing is stored (waste-on-idle policy)
            if (crop.StartedAt is not null || crop.BlockedAt is not null || crop.StoredUnits != 0)
            {
                crop.StartedAt = null;
                crop.BlockedAt = null;
                crop.StoredUnits = 0;
                _needsSaving = true;
            }
            return;
        }

        if (crop.StartedAt is null)
        {
            // Request exists but we haven't started timing yet (should usually be set in Request()).
            crop.StartedAt = dateUsed;
            _needsSaving = true;
            return;
        }

        EnsureRecipeExists(crop.Name);
        var recipe = _recipes.Single(x => x.Item == crop.Name);

        // -------------------- A) BLOCKED MODE (no catch-up while blocked) --------------------
        if (crop.BlockedAt is not null)
        {
            // While blocked, we do NOT accumulate new production time.
            // We only attempt delivery if we already have items stored.
            bool delivered = TryDeliverStoredExactAndWasteRemainderOnComplete(crop, recipe, ref outstanding, out bool completed);
            if (completed)
            {
                crop.StartedAt = null;
                crop.BlockedAt = null;
                crop.StoredUnits = 0;
                _needsSaving = true;
                return;
            }

            // Still blocked if we can't store one full batch (since production creates batches).
            // Note: even though we deliver exact, production still happens in batches.
            bool stillBlocked = crop.StoredUnits > 0 && CanStoreOneBatch(crop, recipe) == false;

            if (stillBlocked)
            {
                if (delivered)
                {
                    _needsSaving = true;
                }
                return;
            }

            // Unblocked now: clear blocked + reset production anchor so blocked duration doesn't create output.
            crop.BlockedAt = null;
            crop.StartedAt = dateUsed;
            _needsSaving = true;
            return;
        }

        // -------------------- B) NORMAL MODE (offline catch-up allowed) --------------------
        TimeSpan elapsed = dateUsed - crop.StartedAt.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        TimeSpan perCycle = GetProductionTimePerItemAdjusted(crop, recipe);
        if (perCycle <= TimeSpan.Zero)
        {
            return;
        }

        int producedCycles = (int)Math.Floor(elapsed.TotalSeconds / perCycle.TotalSeconds);
        if (producedCycles <= 0)
        {
            return;
        }

        ApplyAugmentationForCycles(crop, producedCycles);
        // IMPORTANT: Advance StartedAt by the exact time consumed, preserving remainder.
        crop.StartedAt = crop.StartedAt.Value.AddSeconds(producedCycles * perCycle.TotalSeconds);

        int batchSize = GetBatchSize(crop, recipe);

        // Produce items (StoredUnits is ITEMS, not cycles)
        crop.StoredUnits += producedCycles * batchSize;

        // Deliver exact requested amount; waste remainder if request completes
        bool deliveredAny = TryDeliverStoredExactAndWasteRemainderOnComplete(crop, recipe, ref outstanding, out bool completedRequest);

        if (completedRequest)
        {
            crop.StartedAt = null;
            crop.BlockedAt = null;
            crop.StoredUnits = 0;
            _needsSaving = true;
            return;
        }

        // If we still have items stored but can't store one more batch, block.
        // (Because if inventory can't take a batch, the next production event would be invalid)
        if (crop.StoredUnits > 0 && CanStoreOneBatch(crop, recipe) == false)
        {
            crop.BlockedAt = dateUsed;
        }

        _needsSaving = true;
    }
    /// <summary>
    /// Deliver exact requested amount (items), wasting any extras once request is complete.
    /// StoredUnits is ALWAYS "items waiting to be delivered", never cycles.
    /// </summary>
    private bool TryDeliverStoredExactAndWasteRemainderOnComplete(
        CropAutomationStateModel crop,
        CropRecipe recipe,
        ref int outstanding,
        out bool completedRequest)
    {
        completedRequest = false;
        if (outstanding <= 0)
        {
            if (crop.StoredUnits > 0)
            {
                crop.StoredUnits = 0;
            }

            if (crop.StoredExtraUnits > 0)
            {
                crop.StoredExtraUnits = 0;
            }

            completedRequest = true;
            return false;
        }

        if (crop.StoredUnits <= 0)
        {
            return false;
        }

        int toDeliver = Math.Min(outstanding, crop.StoredUnits);
        if (toDeliver <= 0)
        {
            return false;
        }

        // Build reward bundle: base + optional extras
        BasicList<ItemAmount> bundle = [];
        bundle.Add(new(recipe.Item, toDeliver));

        int extrasToDeliver = 0;
        int addToDelivery = 0;
        if (crop.StoredExtraReward is not null && crop.StoredExtraUnits > 0)
        {
            // for crops you said "always just one", but now it can be multiple cycles,
            // so deliver as many as you have stored (or choose a cap if you want).
            //extrasToDeliver = Math.Min(crop.StoredExtraUnits, toDeliver); // common-sense coupling
            extrasToDeliver = crop.StoredExtraUnits; //maybe this.
            if (extrasToDeliver > 0)
            {
                bundle.Add(new(crop.StoredExtraReward, extrasToDeliver));
            }
            if (crop.StoredExtraReward == recipe.Item)
            {
                addToDelivery = extrasToDeliver;
            }
        }

        if (inventory.CanAcceptRewards(bundle) == false)
        {
            return false;
        }

        inventory.Add(recipe.Item, toDeliver);
        crop.StoredUnits -= toDeliver;
        crop.DeliveredTowardRequest += toDeliver;
        outstanding -= toDeliver - addToDelivery;

        if (extrasToDeliver > 0 && crop.StoredExtraReward is not null)
        {
            inventory.Add(crop.StoredExtraReward, extrasToDeliver);
            crop.StoredExtraUnits -= extrasToDeliver;
        }

        if (outstanding <= 0)
        {
            // waste remainder per your policy
            crop.StoredUnits = 0;
            crop.StoredExtraUnits = 0;
            completedRequest = true;
        }

        return true;
    }
    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }

        DateTime now = DateTime.Now;
        _crops.ForConditionalItems(x => x.IsSuppressed == false && x.Unlocked == true, crop =>
        {
            UpdateTick(crop, now);
        });

        await SaveCropsAsync();
    }
    private async Task SaveCropsAsync()
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
            await _profileService.SaveAsync(_crops);
        }
    }
    public string GetDuration(CropAutomationView summary)
    {
        var crop = _crops.Single(x => x.Name == summary.CropName);
        var recipe = _recipes.Single(x => x.Item == summary.CropName);
        var time = GetProductionTimePerItemAdjusted(crop, recipe);
        return time.GetTimeString;
    }
    private TimeSpan GetProductionTimePerItemAdjusted(CropAutomationStateModel crop, CropRecipe recipe)
    {
        if (crop is null)
        {
            throw new CustomBasicException("Still needed because future");
        }
        double baseM = balanceManager.Base.CropTimeMultiplier;

        double? speedBonus = advancedUpgradeAutomationManager.GetTimeReductionBenefit(crop.Name);
        double bonusM = speedBonus.SpeedBonusToTimeMultiplier(false);

        double m = baseM * bonusM;
        TimeSpan duration = recipe.Duration;
        var reduction = timedBoostManager.GetReducedTime(crop.Name);
        if (reduction < TimeSpan.Zero)
        {
            reduction = TimeSpan.Zero;
        }
        duration -= reduction;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }
        // Per-item time adjustment (with your min 2s rule)
        return duration.Apply(m, false);
    }
    //public TimeSpan GetTimeForGivenCrop(string name)
    void ICropManager.ApplyCropProgressionUnlocks(CropProgressionPlanModel plan, int level)
    {
        bool changed = false;

        lock (_lock)
        {

            foreach (var rule in plan.UnlockRules.Where(r => r.LevelRequired <= level))
            {
                var def = _crops.SingleOrDefault(x => x.Name == rule.ItemName) ?? throw new CustomBasicException($"Crop definition '{rule.ItemName}' was not preloaded.");
                if (def.Unlocked == false)
                {
                    def.Unlocked = true;
                    changed = true;
                }
            }

            if (changed)
            {
                _needsSaving = true;
            }
        }
    }
    TimeSpan ICropManager.GetTimeForGivenCrop(string name)  => _recipes.Single(x => x.Item == name).Duration;

}
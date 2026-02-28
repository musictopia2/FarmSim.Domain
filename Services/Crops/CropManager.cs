namespace FarmSim.Domain.Services.Crops;
public class CropManager(InventoryManager inventory,
    BalanceManager balanceManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    ) : IScenarioResettable, ICropManager
{
    private readonly BasicList<CropInstance> _crops = [];
    private readonly Lock _lock = new();
    private BasicList<CropRecipe> _recipes = [];
    private BasicList<CropDataModel> _allCropDefinitions = [];
    public event Action<ItemAmount>? OnAugmentedOutput;
    private ICropRepository _cropRepository = null!;
    private DateTime _lastSave = DateTime.MinValue;
    private bool _needsSaving;
    private double _offset;
    private BasicList<CropUpgradeTierRule> _upgradeTierRules = [];
    public BasicList<string> UnlockedRecipes => _allCropDefinitions.Where(x => x.Unlocked && x.IsSuppressed == false).Select(x => x.Item).ToBasicList(); //so if you change the list, won't change this.
    public BasicList<Guid> GetUnlockedCrops => _crops.Where(x => x.Unlocked).Select(x => x.Id).ToBasicList();
    public void SetCropSuppressionByProducedItem(string itemName, bool supressed)
    {
        _allCropDefinitions.ForConditionalItems(x => x.Item == itemName, item =>
        {
            item.IsSuppressed = supressed;
        });
        _needsSaving = true;
    }
    public void ApplyCropProgressionUnlocks(CropProgressionPlanModel plan, int level)
    {
        bool changed = false;

        lock (_lock)
        {
            // ---------- SLOTS ----------
            int shouldBeUnlocked = plan.SlotLevelRequired.Count(x => x <= level);

            // Current unlocked (from runtime state)
            int currentlyUnlocked = _crops.Count(x => x.Unlocked);

            // Unlock only the delta, in order, by flipping the first locked ones.
            int toUnlock = shouldBeUnlocked - currentlyUnlocked;
            if (toUnlock > 0)
            {
                foreach (var slot in _crops.Where(x => x.Unlocked == false))
                {
                    slot.Unlocked = true;
                    changed = true;

                    toUnlock--;
                    if (toUnlock == 0)
                    {
                        break;
                    }
                }
            }

            // Optional safety: if your plan expects more unlocked than you even have slots, that's a data bug.
            if (shouldBeUnlocked > _crops.Count)
            {
                throw new CustomBasicException(
                    $"Crop plan expects {shouldBeUnlocked} unlocked slots at level {level}, but only {_crops.Count} slots exist."
                );
            }

            foreach (var rule in plan.UnlockRules.Where(r => r.LevelRequired <= level))
            {
                var def = _allCropDefinitions.SingleOrDefault(x => x.Item == rule.ItemName) ?? throw new CustomBasicException($"Crop definition '{rule.ItemName}' was not preloaded.");
                if (def.Unlocked == false)
                {
                    def.Unlocked = true;
                    inventory.Add(def.Item, 10); //for now, gets 10 of the item for unlocking it.
                    changed = true;
                }
            }

            if (changed)
            {
                _needsSaving = true;
            }
        }
    }
    public EnumCropState GetCropState(Guid id) => GetCrop(id).State;
    public string GetTimeLeft(Guid id) => GetCrop(id).ReadyTime?.GetTimeString ?? "";
    public string GetCropName(Guid id) => GetCrop(id).Crop!;
    public bool IsFast(string name) => _recipes.Single(x => x.Item == name).IsFast;
    private CropInstance GetCrop(Guid id)
    => _crops.SingleOrDefault(x => x.Id == id) ??
       throw new CustomBasicException($"Crop with ID {id} not found.");

    //any methods needed goes here.

    public bool CanManuallyPickUpCrop => advancedSettingsManager.Settings.AutomateCropCollection == false;
    public string GetAdjustedTimeForGivenCrop(string name)
    {
        CropRecipe currentRecipe = _recipes.Single(x => x.Item == name);

        TimeSpan reducedBy = timedBoostManager.GetReducedTime(name);

        var def = _allCropDefinitions.Single(x => x.Item == name);

        bool canInstant = def.MaxBenefits == true && currentRecipe.IsFast;

        double speedBonusMultiplier = def.AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);

        // final multiplier: base profile multiplier * speed-bonus-as-multiplier
        double m = _offset * speedBonusMultiplier;

        TimeSpan duration = currentRecipe.Duration - reducedBy;

        return duration.Apply(m, canInstant).GetTimeString;
    }
    public TimeSpan GetTimeForGivenCrop(string name) => _recipes.Single(x => x.Item == name).Duration;
    public bool CanPlant(Guid id, string item)
    {
        lock (_lock)
        {
            CropInstance crop = GetCrop(id);
            if (crop == null || crop.State != EnumCropState.Empty)
            {
                return false;
            }

            // Normal case: have at least 1 of the crop
            if (inventory.GetInventoryCount(item) > 0)
            {
                return true;
            }

            // Special rule: if player has 0 of a crop but none are currently growing,
            // allow planting one to avoid locking the game.
            // Special rule: allow planting one if nothing is growing
            var anyGrowing = _crops.Any(f => f.State == EnumCropState.Growing && f.Crop == item);
            return anyGrowing == false;
        }
    }
    public bool HasCrop(string item)
    {
        return _recipes.Exists(x => x.Item == item);
    }
    public int GetLevel(string item) => _allCropDefinitions.Single(x => x.Item == item).Level;
    public int LevelRequiredForUpgrade(string item, int levelDesired)
    {
        var rule = _upgradeTierRules.Single(x => x.CropName == item);
        //var recipe = _recipes.Single(x => x.Item == item);
        return rule.TierLevels[levelDesired - 2];
    }
    public void UpdateCropLevel(string item, double extraSpeed, bool maxBenefits)
    {
        var temp = _allCropDefinitions.Single(x => x.Item == item);
        temp.Level++;
        temp.AdvancedSpeedBonus = extraSpeed;
        temp.MaxBenefits = maxBenefits;
        _needsSaving = true;
    }
    public BasicList<GrantableItem> GetUnlockedCropGrantItems()
    {

        BasicList<GrantableItem> output = [];

        _allCropDefinitions.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, temp =>
        {
            output.Add(new()
            {
                Amount = 2,
                Category = EnumItemCategory.Crop,
                Item = temp.Item,
                Source = temp.Item //i think.
            });
        });
        return output;
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
        if (count > 0)
        {
            AddExtraRewards(fins.ExtraRewards.Single(), count);
        }
        AddCrop(item.Item, item.Amount);
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

        maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
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
        bundles.Add(new (item.Item, granted));
        if (fins.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Must have one reward");
        }
        bundles.Add(new (fins.ExtraRewards.Single(), 1)); //will assume you will receive even if no guarantees.
        return inventory.CanAcceptRewards(bundles);
    }
    public void GrantCropItems(GrantableItem item, int toUse)
    {
        if (CanGrantCropItems(item, toUse) == false)
        {
            throw new CustomBasicException("Cannot grant crop items.  Should had called the CanGrantCropItems first");
        }
        bool maxed;

        maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
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
    public void Plant(Guid id, string item)
    {
        lock (_lock)
        {

            if (CanPlant(id, item) == false)
            {
                throw new CustomBasicException("Cannot plant.  Should had called the CanPlant first");
            }
            CropInstance crop = GetCrop(id);
            CropDataModel extras = _allCropDefinitions.Single(x => x.Item == item);
            if (inventory.GetInventoryCount(item) > 0 && extras.MaxBenefits == false)
            {
                inventory.Consume(item, 1); //part of the benefit is not having to use any crops no matter what.
            }
            CropRecipe temp = _recipes.Single(x => x.Item == item);
            TimeSpan reducedBy = timedBoostManager.GetReducedTime(item);
            
            crop.Plant(item, temp, reducedBy, extras.MaxBenefits, extras.AdvancedSpeedBonus);
            _needsSaving = true;
            // Deduct crop only if available; do not go negative. If Wheat == 0
            // and CanPlant allowed due to no growing fields, permit the plant without deduction.
        }
    }
    public bool CanHarvest(Guid id)
    {
        lock (_lock)
        {
            
            CropInstance crop = GetCrop(id);
            if (crop.Crop is null)
            {
                throw new CustomBasicException("No crop");
            }
            BasicList<ItemAmount> bundle = [];
            // base

            int amount;
            amount = 2;
            if (crop.MaxedBenefits!.Value == true)
            {
                amount++;
            }

            bundle.Add(new ItemAmount(crop.Crop, amount));
            // extras (already resolved on instance)
            if (crop.ExtraRewards is not null && crop.ExtraRewards.Count > 0)
            {
                bundle.AddRange(crop.ExtraRewards);
            }
            
            return inventory.CanAcceptRewards(bundle);
        }
    }
    public void Harvest(Guid id)
    {
        lock (_lock)
        {
            CropInstance crop = GetCrop(id);
            Harvest(crop);
        }
    }
    private void AddExtraRewards(string item, int amount)
    {
        ItemAmount payLoad = new()
        {
            Amount = amount,
            Item = item
        };
        OnAugmentedOutput?.Invoke(payLoad);
        inventory.Add(payLoad);
    }
    private void Harvest(CropInstance crop)
    {
        if (crop.Crop is null)
        {
            throw new CustomBasicException("No crop");
        }
        if (crop.ExtraRewards.Count > 0)
        {
            AddExtraRewards(crop.ExtraRewards.Single().Item, crop.ExtraRewards.Single().Amount);
        }
        int amount = 2;
        if (crop.MaxedBenefits!.Value)
        {
            amount++;
        }
        AddCrop(crop.Crop, amount);
        crop.Clear();
    }
    private void AddCrop(string item, int amount)
    {
        inventory.Add(item, amount);
        _needsSaving = true;
    }
    public async Task SetStyleContextAsync(CropServicesContext context, FarmKey farm)
    {
        _cropRepository = context.CropRepository;
        _recipes = await context.CropRecipes.GetCropsAsync();
        _upgradeTierRules = await context.CropUpgradeTierRules.GetRulesAsync(farm);
        foreach (var recipe in _recipes)
        {
            itemRegistry.Register(new(recipe.Item, EnumInventoryStorageCategory.Silo, EnumInventoryItemCategory.Crops));

        }
        CropSystemState system = await context.CropRepository.LoadAsync();
        _crops.Clear();
        _offset = balanceManager.Base.CropTimeMultiplier;
        system.Slots.ForEach(x =>
        {
            CropRecipe? recipe = null;
            if (x.State == EnumCropState.Growing)
            {
                //
                recipe = _recipes.Single(y => y.Item == x.Crop);
            }
            CropInstance crop = new(_offset, recipe, timedBoostManager, outputAugmentationManager);
            crop.Load(x);
            _crops.Add(crop);
        });
        _allCropDefinitions = system.Crops.ToBasicList();
    }
    public async Task UpdateTickAsync()
    {
        
        _crops.ForConditionalItems(x => x.Unlocked && x.Crop is not null,
            (crop) =>
        {
            crop.UpdateTick();
            if (advancedSettingsManager.Settings.AutomateCropCollection && crop.State == EnumCropState.Ready)
            {
                //for now, can always do.  later will change.
                if (inventory.CanAdd(crop.Crop!, 2))
                {
                    Harvest(crop);
                }
            }
            if (crop.NeedsToSave)
            {
                _needsSaving = true;
            }
        });
        if (_needsSaving)
        {
            await SaveCropsAsync();
        }
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
            BasicList<CropAutoResumeModel> list = _crops.Select(crop => new CropAutoResumeModel
            {
                Unlocked = crop.Unlocked,
                Crop = crop.Crop,
                State = crop.State,
                PlantedAt = crop.PlantedAt,
                RunMultiplier = crop.GetCurrentRun,
                ReducedBy = crop.ReducedBy,
                ExtraRewards = crop.ExtraRewards,
                ExtrasResolved = crop.IsExtrasResolved,
                OutputPromise = crop.OutputPromise,
                MaxedBenefits = crop.MaxedBenefits,
                AdvancedSpeedBonus = crop.AdvancedSpeedBonus
            }).ToBasicList();
            //has to figure out the other side (since you may unlock more slots).
            CropSystemState slate = new()
            {
                Slots = list,
                Crops = _allCropDefinitions
            };
            await _cropRepository.SaveAsync(slate);
        }
    }

    void IScenarioResettable.ResetForScenario()
    {
        lock (_lock)
        {
            foreach (var crop in _crops)
            {
                crop.Clear();

            }

            _needsSaving = true;
        }
    }
}
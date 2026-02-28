
using System.Reflection.Metadata;

namespace FarmSim.Domain.Services.Animals;
public class AnimalManager(InventoryManager inventory,
    BalanceManager balanceManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    ) : IScenarioResettable, IAnimalManager
{
    private readonly BasicList<AnimalInstance> _animals = [];
    public event Action? OnAnimalsUpdated;
    public event Action<string>? OnAnimalCollected;
    public event Action<ItemAmount>? OnAugmentedOutput;
    private IAnimalRepository _animalRepository = null!;
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private readonly Lock _lock = new();
    private BasicList<AnimalRecipe> _recipes = [];
    private double _offset;
    private BasicList<AnimalUpgradeTierRule> _upgradeTierRules = [];
    public bool IsFast(AnimalView animal) => _recipes.Single(x => x.Animal == animal.Name).IsFast;
    public TimeSpan GetTimePerOutputForGivenAnimal(string outputName)
    {
        foreach (var item in _recipes)
        {
            var firsts = item.Options.FirstOrDefault(x => x.Output.Item == outputName);
            if (firsts is not null)
            {
                return firsts.Duration / firsts.Output.Amount;
            }
        }
        throw new CustomBasicException($"Did not find {outputName} for figuring out the time per output");
    }
    public int LevelRequiredForUpgrade(AnimalView animal, int levelDesired)
    {
        var upgrade = _upgradeTierRules.Single(x => x.AnimalName == animal.Name);
        return upgrade.TierLevels[levelDesired - 2];
    }
    //only show for first time.
    public string GetAdjustedTimeForGivenAnimal(AnimalView animal)
    {
        AnimalRecipe currentRecipe = _recipes.Single(x => x.Animal == animal.Name);
        AnimalInstance instance = GetAnimalById(animal);
        TimeSpan reducedBy = timedBoostManager.GetReducedTime(animal.Name);


        bool canInstant = instance.MaxBenefits == true && instance.IsFast;

        double speedBonusMultiplier = instance.AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);

        // final multiplier: base profile multiplier * speed-bonus-as-multiplier
        double m = _offset * speedBonusMultiplier;

        TimeSpan duration = currentRecipe.Options.First().Duration - reducedBy;

        return duration.Apply(m, canInstant).GetTimeString;
    }
    public BasicList<AnimalView> GetUnlockedAnimals
    {
        get
        {
            BasicList<AnimalView> output = [];

            _animals.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, t =>
            {
                AnimalView summary = new()
                {
                    Id = t.Id,
                    Name = t.Name,
                    IsRental = t.IsRental,
                    MaxBenefits = t.MaxBenefits,
                    IsFast = t.IsFast
                };
                output.Add(summary);
            });
            return output;
        }
    }
    public AnimalProductionOption NextProductionOption(string animal)
    {
        var instance = _animals.First(x => x.Name == animal);
        var option = instance.NextProductionOption;
        var key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(animal);
        if (key is null)
        {
            return option;
        }
        var snap = outputAugmentationManager.GetSnapshot(key);
        if (snap.IsDouble == false)
        {
            return option;
        }
        return new()
        {
            Duration = option.Duration,
            Input = option.Input,
            Required = option.Required,
            Output = new ItemAmount(option.Output.Item, option.Output.Amount * 2)
        };
    }

    public void SetAnimalSuppressionByProducedItem(string itemName, bool supressed)
    {
        _animals.ForEach(x =>
        {
            if (x.GetUnlockedProductionOptions().All(x => x.Output.Item == itemName))
            {
                x.IsSuppressed = supressed;
            }
        });
        _needsSaving = true;
    }
    public Guid StartRental(StoreItemRowModel rental)
    {
        if (rental.Category != EnumCatalogCategory.Animal)
        {
            throw new CustomBasicException("Only animals can be rented");
        }
        var instance = _animals.LastOrDefault(x => x.Name == rental.TargetName && x.Unlocked == false)
            ?? throw new CustomBasicException("No locked animal available to rent");
        instance.Unlocked = true;
        //instance.State = EnumAnimalState.Collecting; //i think.
        instance.IsRental = true; //so later can lock the proper one.  also ui can show the details for it as well.
        _needsSaving = true;
        return instance.Id;
    }

    public bool CanDeleteRental(Guid id, string targetName)
    {
        AnimalInstance? animal = _animals.SingleOrDefault(x => x.Id == id);
        if (animal is not null)
        {
            if (animal.Unlocked == false)
            {
                return true;
            }
            if (animal.IsRental == false)
            {
                animal.IsRental = true; //implies its a rental.
                _needsSaving = true;
            }
            if (animal.State == EnumAnimalState.None)
            {
                animal.Unlocked = false;
                _needsSaving = true;
            }
            return false;
        }
        var rentals = _animals.Where(x => x.IsRental && x.Name == targetName).ToList();
        if (rentals.Count != 1)
        {
            throw new InvalidOperationException($"Invariant violated: expected exactly one rental animal, found {rentals.Count}.");
        }

        animal = rentals.First();
        if (animal.Unlocked == false)
        {
            return true;
        }
        animal.Unlocked = false;
        _needsSaving = true;
        return false;

    }

    public void DoubleCheckActiveRental(Guid id)
    {
        AnimalInstance? animal = _animals.SingleOrDefault(x => x.Id == id);
        if (animal is null)
        {
            return; //for now.
        }
        if (animal.IsRental == false)
        {
            animal.IsRental = true;
            _needsSaving = true;
        }
        if (animal.Unlocked == false)
        {
            animal.Unlocked = true;
            _needsSaving = true;
        }
    }


    public void UnlockAnimalPaidFor(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Animal)
        {
            throw new CustomBasicException("Only animals can be paid for");
        }
        var instance = _animals.First(x => x.Name == store.TargetName && x.Unlocked == false && x.IsRental == false);
        instance.Unlocked = true;


        instance.State = EnumAnimalState.Collecting;

        var recipe = _recipes.Single(x => x.Animal == store.TargetName);
        instance.UpdateReady(recipe.Options.First().Output.Amount);


        _needsSaving = true;
    }
    public void ApplyAnimalProgressionUnlocksFromLevels(
        BasicList<ItemUnlockRule> rules,
        BasicList<CatalogOfferModel> offers,
        int level)
    {
        bool changed = false;

        lock (_lock)
        {
            // -----------------------------
            // 1) Type-level counts by animal
            // -----------------------------

            // First option unlocked comes from offers (per animal type).
            // We treat "has any offer <= level" as "base option unlocked".
            // IMPORTANT: This does NOT mean the animal itself is unlocked.
            var hasBaseOption = offers
                .Where(o => o.LevelRequired <= level)
                .GroupBy(o => o.TargetName)
                .ToDictionary(g => g.Key, g => true);

            // Extra options come from rules (duplicates = extra unlock tokens)
            var extraCounts = rules
                .Where(r => r.LevelRequired <= level)
                .GroupBy(r => r.ItemName)
                .ToDictionary(g => g.Key, g => g.Count());

            // Union of all animal names that appear in either system
            var animalNames = hasBaseOption.Keys
                .Union(extraCounts.Keys)
                .ToBasicList();

            foreach (var animalName in animalNames)
            {
                // All instances (could be 0, 1, or many)
                var instances = _animals.Where(a => a.Name == animalName).ToBasicList();
                if (instances.Count == 0)
                {
                    // Hard invariant violated: plan refers to animal not preloaded
                    throw new CustomBasicException($"Animal instances not preloaded for '{animalName}'.");
                }

                int baseAllowed = hasBaseOption.ContainsKey(animalName) ? 1 : 0;
                int extraEarned = extraCounts.TryGetValue(animalName, out int extra) ? extra : 0;

                // Determine cap (assumes all instances share same TotalProductionOptions)
                int cap = instances[0].TotalProductionOptions;

                int desiredAllowed = Math.Min(baseAllowed + extraEarned, cap);

                // Apply to ALL instances, even if locked (so UI can show options)
                foreach (var animal in instances)
                {
                    if (animal.ProductionOptionsAllowed != desiredAllowed)
                    {
                        animal.ProductionOptionsAllowed = desiredAllowed;
                        changed = true;
                    }
                }
            }

            // -----------------------------------------
            // 2) Optional: unlock ONE instance by offer
            // -----------------------------------------
            // If your offers also represent "free unlock at level",
            // you can unlock a single locked instance here.
            //
            // If some animals are purchase-gated, you MUST gate this
            // with your own condition (example: offer is free).
            //
            // If you *never* want this method to unlock animals anymore,
            // you can delete this entire section.


            var offer = offers.FirstOrDefault(x => x.LevelRequired == level);
            if (offer is not null)
            {
                string animalName = offer.TargetName;


                var instance = _animals.First(a => a.Name == animalName && a.Unlocked == false);


                instance.Unlocked = true;
                instance.State = EnumAnimalState.Collecting;

                var recipe = _recipes.Single(x => x.Animal == animalName);
                instance.UpdateReady(recipe.Options.First().Output.Amount);

                changed = true;
            }


            if (changed)
            {
                _needsSaving = true;
            }
        }

        if (changed)
        {
            OnAnimalsUpdated?.Invoke();
        }
    }

    private AnimalInstance GetAnimalById(Guid id)
    {
        var tree = _animals.SingleOrDefault(t => t.Id == id) ?? throw new CustomBasicException($"Animal with Id {id} not found.");
        return tree;
    }
    private AnimalInstance GetAnimalById(AnimalView id) => GetAnimalById(id.Id);
    public bool HasAnimal(string item)
    {
        bool rets = false;
        _recipes.ForEach(recipe =>
        {
            if (rets == true)
            {
                return;
            }
            if (recipe.Options.Any(x => x.Output.Item == item))
            {
                rets = true;
            }
        });
        return rets;
    }


    public bool CanGrantUnlimitedAnimalItems(GrantableItem item)
    {
        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Source);
        if (temp is null)
        {
            return inventory.CanAdd(item.Item, item.Amount);
        }
        var fins = outputAugmentationManager.GetSnapshot(temp);
        if (fins.ExtraRewards.Single() == item.Item)
        {
            return inventory.CanAdd(item.Item, item.Amount);
        }
        if (fins.IsDouble)
        {
            return inventory.CanAdd(item.Item, item.Amount); //ignored for this.
        }
        if (fins.ExtraRewards.Count > 1)
        {
            throw new CustomBasicException("Should be no extra rewards on animal items except for one");
        }

        if (fins.Chance >= 100)
        {
            throw new CustomBasicException("Should be no guarantees on animal items");
        }

        //has to figure out the chance stuff here.
        //has to predetermine what is going to happen here.
        int bonus = rs1.ComputeUnlimitedBonus(item.Amount, fins.Chance);
        if (bonus == 0)
        {
            return inventory.CanAdd(item.Item, item.Amount);
        }
        BasicList<ItemAmount> list = [];
        list.Add(new ItemAmount(item.Item, item.Amount));
        list.Add(new()
        {
            Item = fins.ExtraRewards.Single(),
            Amount = bonus
        });
        return inventory.CanAcceptRewards(list);

    }
    public void GrantUnlimitedAnimalItems(GrantableItem item)
    {
        if (item.Category != EnumItemCategory.Animal)
        {
            throw new CustomBasicException("This is not an animal");
        }
        if (CanGrantUnlimitedAnimalItems(item) == false)
        {
            throw new CustomBasicException("Cannot grant unlimited animal items.  Should had ran the CanGrantUnlimitedAnimalItems function first");
        }
        //this will not use speed seeds or have any requirements.
        //if (inventory.CanAdd(item) == false)
        //{
        //    throw new CustomBasicException("Unable to add because was full.  Should had ran the required functions first");
        //}
        //hopefully no problem with requiring security (?) since this is intended for the unlimited feature.
        OnAnimalCollected?.Invoke(item.Source); //no matter how many you ask for, only counts as 1.   so if you have to do several times, has to manually be done.
        //if i wanted automation, then via rest api.
        //if i really wanted to abuse the api just for the reward, i can (but would be hard to create and maintain).   if i somehow don't have enough, would be in for a bad surprise


        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.Source);
        if (temp is null)
        {
            AddAnimalToInventory(item.Item, item.Amount);
            return;

        }
        var fins = outputAugmentationManager.GetSnapshot(temp);
        if (fins.ExtraRewards.Count > 1)
        {
            throw new CustomBasicException("Should be no extra rewards on animal items except for one");
        }
        if (fins.IsDouble)
        {
            AddAnimalToInventory(item.Item, item.Amount);
            return;
        }
        if (fins.ExtraRewards.Single() == item.Item)
        {
            AddAnimalToInventory(item.Item, item.Amount);
            return;
        }
        int bonus = rs1.ComputeUnlimitedBonus(item.Amount, fins.Chance);
        if (bonus > 0)
        {
            AddExtraRewards(fins.ExtraRewards.Single(), bonus);

        }
        AddAnimalToInventory(item.Item, item.Amount);
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


    private static BasicList<ItemAmount> BuildSpeedSeedRewardBundleWorstCase(
        AnimalGrantModel item,
        int granted,
        OutputAugmentationSnapshot fins)
    {
        BasicList<ItemAmount> rewards = [];

        // base
        rewards.Add(new ItemAmount(item.OutputData.Item, granted));

        if (fins.IsDouble)
        {
            // Double means base doubles (and/or extras, depending on your plan)
            rewards.Clear();
            rewards.Add(new ItemAmount(item.OutputData.Item, granted * 2));
            return rewards;
        }

        // chance-based extras: worst-case assume they will be awarded
        foreach (var extraItem in fins.ExtraRewards)
        {
            rewards.Add(new ItemAmount(extraItem, 1)); // matches your ResolveExtraRewards payout rule
        }

        return rewards;
    }
    public bool CanGrantAnimalItems(AnimalGrantModel item, int toUse)
    {
        bool maxed;

        maxed = _animals.Any(x => x.MaxBenefits);
        if (toUse <= 0)
        {
            return false;
        }
        if (inventory.Get(CurrencyKeys.SpeedSeed) < toUse)
        {
            return false;
        }
        if (maxed == false)
        {
            if (inventory.Has(item.InputData.Item, item.InputData.Amount * toUse) == false)
            {
                return false;
            }
        }


        int amount;
        amount = item.OutputData.Amount;

        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        if (maxed)
        {
            amount++; //just one more.
        }
        int granted = toUse * amount;

        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.AnimalName);
        if (temp is null)
        {
            return inventory.CanAdd(item.OutputData.Item, granted);
        }
        var fins = outputAugmentationManager.GetSnapshot(temp);
        BasicList<ItemAmount> bundles = BuildSpeedSeedRewardBundleWorstCase(item, granted, fins);
        return inventory.CanAcceptRewards(bundles);

    }
    public void GrantAnimalItems(AnimalGrantModel item, int toUse)
    {
        if (CanGrantAnimalItems(item, toUse) == false)
        {
            throw new CustomBasicException("Cannot grant animal items.  Should had ran the CanGrantAnimalItems function first");
        }
        OnAnimalCollected?.Invoke(item.AnimalName);

        int amount;
        amount = item.OutputData.Amount;
        bool maxed;

        maxed = _animals.Any(x => x.MaxBenefits);
        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        if (maxed)
        {
            amount++; //just one more.
        }
        int granted = toUse * amount;

        if (maxed == false)
        {
            inventory.Consume(item.InputData.Item, item.InputData.Amount * toUse);
        }


        var temp = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.AnimalName);
        if (temp is null)
        {
            AddAnimalToInventory(item.OutputData.Item, granted);
        }
        else
        {
            var fins = outputAugmentationManager.GetSnapshot(temp);
            if (fins.IsDouble)
            {
                AddAnimalToInventory(item.OutputData.Item, granted * 2);
            }
            else
            {

                bool hit = rs1.RollHit(fins.Chance);
                if (fins.ExtraRewards.Count != 1)
                {
                    throw new CustomBasicException("For chanced based. must have just one reward");
                }
                if (hit)
                {
                    AddExtraRewards(fins.ExtraRewards.Single(), 1);
                }
                AddAnimalToInventory(item.OutputData.Item, granted);
            }
        }
        inventory.Consume(CurrencyKeys.SpeedSeed, toUse);
    }

    public int GetDisplayedGrantAmount(AnimalGrantModel item)
    {
        int granted = item.OutputData.Amount;

        var key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(item.AnimalName);
        if (key is null)
        {
            return granted;
        }

        var snap = outputAugmentationManager.GetSnapshot(key);

        // Only treat as deterministic display if it doubles the SAME item
        if (snap.IsDouble
            && snap.ExtraRewards.Count == 1
            && string.Equals(snap.ExtraRewards.Single(), item.OutputData.Item, StringComparison.OrdinalIgnoreCase))
        {
            return granted * 2;
        }

        return granted;
    }

    public BasicList<AnimalGrantModel> GetUnlockedAnimalGrantItems()
    {
        BasicList<AnimalGrantModel> output = [];
        HashSet<string> seenAnimals = [];

        foreach (var animal in _animals)
        {
            // skip locked animals
            if (animal.Unlocked == false)
            {
                continue;
            }
            if (animal.IsSuppressed)
            {
                continue;
            }
            // ensure each animal type is processed once, in original order
            if (seenAnimals.Add(animal.Name) == false)
            {
                continue;
            }

            var options = animal.GetUnlockedProductionOptions();

            foreach (var item in options)
            {
                output.Add(new AnimalGrantModel
                {
                    AnimalName = animal.Name,
                    InputData = new ItemAmount
                    {
                        Item = item.Required,
                        Amount = item.Input
                    },
                    OutputData = item.Output
                });
            }
        }
        return output;
    }
    public bool IsAnimalFast(AnimalView animal) => _recipes.Single(x => x.Animal == animal.Name).IsFast;

    public int GetLevel(AnimalView animal)
    {
        AnimalInstance instance = GetAnimalById(animal);
        return instance.Level;
    }
    public void UpgradeAnimalLevel(AnimalView animal, double extraSpeed, bool maxBenefits)
    {
        AnimalInstance instance = GetAnimalById(animal);
        instance.Level++;
        instance.AdvancedSpeedBonus = extraSpeed;
        instance.MaxBenefits = maxBenefits;
        _needsSaving = true;
    }
    public BasicList<AnimalProductionOption> GetUnlockedProductionOptions(AnimalView animal)
    {
        AnimalInstance instance = GetAnimalById(animal);
        var list = instance.GetUnlockedProductionOptions().ToBasicList();
        if (instance.IsFast && instance.MaxBenefits)
        {
            list = list.Take(1).ToBasicList(); //only one option now.
        }
        var key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(animal.Name);
        if (key is null)
        {
            return list;
        }

        var snap = outputAugmentationManager.GetSnapshot(key);
        if (snap.IsDouble == false)
        {
            return list;
        }
        BasicList<AnimalProductionOption> output = [];
        foreach (var item in list)
        {
            var others = new AnimalProductionOption()
            {
                Input = item.Input,
                Duration = item.Duration,
                Required = item.Required,
                Output = new ItemAmount(item.Output.Item, item.Output.Amount * 2)
            };
            output.Add(others);
        }
        return output;
    }
    public string GetName(AnimalView animal)
    {
        AnimalInstance instance = GetAnimalById(animal);
        return instance.Name;
    }
    public int Required(AnimalView animal, int selected) => GetAnimalById(animal).RequiredCount(selected);
    public int Returned(AnimalView animal, int selected) => GetAnimalById(animal).Returned(selected);
    public bool CanProduce(AnimalView animal, int selected)
    {
        
        AnimalInstance instance = GetAnimalById(animal);
        if (instance.State != EnumAnimalState.None)
        {
            return false;
        }
        if (instance.IsFast && selected > 0 && instance.MaxBenefits)
        {
            return false; //if you are doing the fast option and you maxed the benefits, you cannot produce from anything else.
        }
        if (instance.MaxBenefits)
        {
            return true;
        }
        int required = instance.RequiredCount(selected);
        int count = inventory.Get(instance.RequiredName(selected));
        var output =  count >= required;
        if (selected == 1)
        {

        }
        return output;
    }
    public void Produce(AnimalView animal, int selected)
    {
        AnimalInstance instance = GetAnimalById(animal);
        if (CanProduce(animal, selected) == false)
        {
            throw new CustomBasicException("Cannot produce animal.  Should had used CanProduce function");
        }
        if (instance.MaxBenefits == false)
        {
            int required = instance.RequiredCount(selected);
            inventory.Consume(instance.RequiredName(selected), required);
        }
        string item = instance.ItemReceived(selected);
        TimeSpan reducedBy = timedBoostManager.GetReducedTime(item);
        instance.Produce(selected, reducedBy);
        _needsSaving = true;
    }
    private int GetAmount(AnimalInstance instance)
    {
        int maxs;
        if (advancedSettingsManager.Settings.AnimalCollectionPolicy == EnumAnimalCollectionMode.OneAtTime)
        {
            maxs = 1;
        }
        else
        {
            maxs = instance.OutputReady;
        }
        return maxs;
    }
    private bool CanCollect(AnimalInstance instance)
    {
        int maxs = GetAmount(instance);


        //maxed = _allCropDefinitions.Single(x => x.Item == item.Item).MaxBenefits;
        if (instance.MaxBenefits)
        {
            maxs++; //just one more.
        }
        BasicList<ItemAmount> bundle = [];

        // base
        bundle.Add(new ItemAmount(instance.ReceivedName, maxs));

        // extras (already resolved on instance)
        if (instance.ExtraRewards is not null && instance.ExtraRewards.Count > 0)
        {
            bundle.AddRange(instance.ExtraRewards);
        }
        return inventory.CanAcceptRewards(bundle);
    }
    public bool CanCollect(AnimalView animal)
    {
        if (advancedSettingsManager.Settings.AnimalCollectionPolicy == EnumAnimalCollectionMode.Automated)
        {
            throw new CustomBasicException("Should had been automated");
        }
        AnimalInstance instance = GetAnimalById(animal);
        return CanCollect(instance);

    }
    public void Collect(AnimalView animal)
    {
        //if there is a change in collection mode, requires rethinking.
        //cannot be here because has to have validation that is not async now.

        if (advancedSettingsManager.Settings.AnimalCollectionPolicy == EnumAnimalCollectionMode.Automated)
        {
            throw new CustomBasicException("Should had been automated");
        }
        AnimalInstance instance = GetAnimalById(animal);
        int maxs = GetAmount(instance);
        Collect(instance, maxs);
    }
    private void Collect(AnimalInstance animal, int maxs)
    {
        if (CanCollect(animal) == false)
        {
            throw new CustomBasicException("Not enough storage space to collect (includes bonus rewards).");
        }
        bool wasUnlocked = animal.Unlocked;
        string selectedName = animal.ReceivedName;

        maxs.Times(_ => animal.Collect());



        if (animal.ExtraRewards.Count > 0)
        {
            AddExtraRewards(animal.ExtraRewards.Single().Item, animal.ExtraRewards.Single().Amount);
        }


        if (animal.OutputReady == 0 && animal.MaxBenefits)
        {
            maxs++; //you get one more at the end.
        }


        // base
        AddAnimalToInventory(selectedName, maxs);
        // IMPORTANT: clear extras so you don't add them again next collect
        animal.Clear(); //needed a new method.  otherwise, it would had cleared and would never show extra rewards.

        if (animal.OutputReady == 0)
        {
            OnAnimalCollected?.Invoke(animal.Name);
        }

        if (wasUnlocked && animal.Unlocked == false)
        {
            OnAnimalsUpdated?.Invoke();
        }
    }
    private void AddAnimalToInventory(string name, int amount)
    {
        inventory.Add(name, amount);
        _needsSaving = true;
    }
    public EnumAnimalState GetState(AnimalView animal) => GetAnimalById(animal).State;
    public int Left(AnimalView animal) => GetAnimalById(animal).OutputReady;
    public string TimeLeftForResult(AnimalView animal)
    {
        AnimalInstance instance = GetAnimalById(animal);
        if (instance.ReadyTime is null)
        {
            return "";
        }
        return instance.ReadyTime!.Value.GetTimeString;
    }
    public string Duration(AnimalView animal, int selected)
    {
        AnimalInstance instance = GetAnimalById(animal);
        string item = instance.ItemReceived(selected);
        TimeSpan reducedBy = timedBoostManager.GetReducedTime(item);
        return instance.GetDuration(selected, reducedBy).GetTimeString;
    }
    public int InProgress(AnimalView animal) => GetAnimalById(animal).AmountInProgress;
    public async Task SetStyleContextAsync(AnimalServicesContext context, FarmKey farm)
    {
        if (_animalRepository != null)
        {
            throw new InvalidOperationException("Persistance Already set");
        }
        _upgradeTierRules = await context.AnimalUpgradeTierRule.GetRulesAsync(farm);
        _animalRepository = context.AnimalRepository;
        _recipes = await context.AnimalRecipes.GetAnimalsAsync();
        foreach (var item in _recipes)
        {
            foreach (var temp in item.Options)
            {
                itemRegistry.Register(new(temp.Output.Item, EnumInventoryStorageCategory.Barn, EnumInventoryItemCategory.Animals));
            }
        }
        var ours = await context.AnimalRepository.LoadAsync();
        _animals.Clear();
        _offset = balanceManager.Base.AnimalTimeMultiplier;
        foreach (var item in ours)
        {
            AnimalRecipe recipe = _recipes.Single(x => x.Animal == item.Name);
            AnimalInstance animal = new(recipe, _offset, timedBoostManager, outputAugmentationManager);

            animal.Load(item);
            _animals.Add(animal);
        }
    }
    public async Task UpdateTickAsync()
    {
        _animals.ForConditionalItems(x => x.Unlocked && x.State != EnumAnimalState.None && x.IsSuppressed == false, animal =>
        {
            animal.UpdateTick();
            if (animal.State == EnumAnimalState.Collecting && advancedSettingsManager.Settings.AnimalCollectionPolicy == EnumAnimalCollectionMode.Automated)
            {
                if (CanCollect(animal))
                {
                    Collect(animal, animal.OutputReady); //if you cannot collect, then still can't do.
                }
            }
            if (animal.NeedsToSave)
            {
                _needsSaving = true;
            }
        });
        await SaveAnimalsAsync();
    }
    private async Task SaveAnimalsAsync()
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
            BasicList<AnimalAutoResumeModel> list = _animals
             .Select(animal => animal.GetAnimalForSaving)
             .ToBasicList();
            await _animalRepository.SaveAsync(list);
        }
    }

    void IScenarioResettable.ResetForScenario()
    {
        lock (_lock)
        {
            foreach (var animal in _animals)
            {
                animal.Reset();
            }

            _needsSaving = true;
        }

        // optional
        OnAnimalsUpdated?.Invoke();
    }

    void IAnimalManager.PurchaseAnimal(StoreItemRowModel store)
    {
        UnlockAnimalPaidFor(store);
    }

    int IAnimalManager.GetUnlockedCount(string animalName)
    {
        return _animals.Count(x => x.Name == animalName && x.Unlocked && x.IsSuppressed == false);
    }
}
namespace FarmSim.Domain.Services.Automation.Animals;
public class AnimalAutomationManager(
    InventoryManager inventory,
    BalanceManager balanceManager,
    RulesManager rulesManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager
    ) : IAnimalManager
{
    private bool _init;
    private IAnimalAutomationProfile _profileService = null!;
    private IAnimalBaseRulesProvider _baseRulesProvider = null!;
    private readonly Lock _lock = new();

    private BasicList<AnimalAutomationStateModel> _animals = [];
    private BasicList<AnimalRecipe> _recipes = [];

    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;

    private Dictionary<string, int> _capByAnimal = new(StringComparer.OrdinalIgnoreCase);

    // ------------------------- Public API -------------------------
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
        if (bonus > 10)
        {
            bonus = 10; //to stop abuse.
        }
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
        //OnAugmentedOutput?.Invoke(payLoad);
        inventory.Add(payLoad);
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
            if (seenAnimals.Add(animal.AnimalName) == false)
            {
                continue;
            }
            var recipe = _recipes.Single(x => x.Animal == animal.AnimalName);
            output.Add(new AnimalGrantModel
            {
                AnimalName = animal.AnimalName,
                InputData = new ItemAmount
                {
                    Item = recipe.Options.First().Required,
                    Amount = recipe.Options.First().Input,
                },
                OutputData = recipe.Options.First().Output
            });


            
        }
        return output;
    }
    public bool CanGrantAnimalItems(AnimalGrantModel item, int toUse)
    {
        bool maxed;
        maxed = false;
        //maxed = _animals.Any(x => x.MaxBenefits);
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
        //OnAnimalCollected?.Invoke(item.AnimalName);

        int amount;
        amount = item.OutputData.Amount;
        bool maxed;
        maxed = false;
        //maxed = _animals.Any(x => x.MaxBenefits);
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

    private void AddAnimalToInventory(string name, int amount)
    {
        inventory.Add(name, amount);
        _needsSaving = true;
    }
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

    public async Task SetStyleContextAsync(AnimalAutomationServicesContext context, FarmKey farm)
    {
        if (rulesManager.AutomationEnabled == false)
        {
            return;
        }
        _profileService = context.AnimalAutomationProfile;
        _baseRulesProvider = context.AnimalBaseRulesProvider;
        _recipes = await context.AnimalRecipes.GetAnimalsAsync();
        foreach (var item in _recipes)
        {
            foreach (var temp in item.Options)
            {
                itemRegistry.Register(new(temp.Output.Item, EnumInventoryStorageCategory.Barn, EnumInventoryItemCategory.Animals));
            }
        }
        var rules = await _baseRulesProvider.GetRulesAsync(farm);
        // expected shape: rules has AnimalName + StartingQueueCount (same idea as crops/trees)
        _capByAnimal = rules.ToDictionary(x => x.AnimalName, x => x.StartingQueueCount, StringComparer.OrdinalIgnoreCase);

        _animals = await _profileService.LoadAsync();
        _init = true;
    }
    private AnimalRecipe GetRecipeByProducedItem(string produced)
    {
        foreach (var item in _recipes)
        {
            if (item.Options.First().Output.Item == produced)
            {
                return item;
            }
        }
        throw new CustomBasicException($"No recipe with {produced}");
    }
    public void SetAnimalSuppressionByProducedItem(string produced, bool suppressed)
    {
        var recipe = GetRecipeByProducedItem(produced);
        var s = _animals.Single(x => x.AnimalName == recipe.Animal);
        s.IsSuppressed = suppressed;
        _needsSaving = true;
    }
    public bool IsSuppressed(string animalName) => _animals.Single(x => x.AnimalName == animalName).IsSuppressed;
    public void ApplyAnimalUnlocksOnLevels(BasicList<CatalogOfferModel> offers, int level)
    {
        var item = offers.FirstOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }

        var s = _animals.Single(x => x.AnimalName == item.TargetName);
        if (s.Unlocked == false)
        {
            s.Unlocked = true;
            _needsSaving = true;
        }
    }

    public void Request(string animalName, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            return;
        }

        EnsureRecipeExists(animalName);

        int cap = GetCap(animalName);
        if (cap <= 0)
        {
            return;
        }

        var s = _animals.Single(x => x.AnimalName == animalName);

        int remainingCapacity = GetRemainingCapacity(animalName);
        int add = Math.Min(amountToAdd, remainingCapacity);
        if (add <= 0)
        {
            return;
        }

        bool wasInactive = (s.RequestedTotal - s.DeliveredTowardRequest) <= 0;

        s.RequestedTotal += add;

        if (wasInactive)
        {
            s.StoredUnits = 0;
            s.BlockedAt = null;
            s.StartedAt = null;
            s.NextReadyAt = null;

            var option = GetSelectedOption(animalName);
            TryStartCycleAt(s, option, DateTime.Now);
        }

        _needsSaving = true;
    }
    private bool TryStartCycleAt(AnimalAutomationStateModel animal, AnimalProductionOption option, DateTime startTime)
    {
        int outstanding = animal.RequestedTotal - animal.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            animal.NextReadyAt = null;
            return false;
        }

        int feedPerCycle = GetFeedPerCycle(animal, option);
        if (feedPerCycle > 0)
        {
            if (inventory.Get(option.Required) < feedPerCycle)
            {
                animal.BlockedAt = startTime;   // block immediately at the moment we tried to start
                animal.NextReadyAt = null;
                animal.StartedAt = null;
                return false;
            }

            inventory.Consume(option.Required, feedPerCycle); // pay for this cycle NOW
        }

        TimeSpan perCycle = GetProductionTimePerCycleAdjusted(option);

        animal.BlockedAt = null;
        animal.StartedAt = startTime;                   // optional for UI
        animal.NextReadyAt = startTime.Add(perCycle);   // running & paid
        return true;
    }
    public int GetVirtualCount(string animalName)
    {
        var animal = _animals.Single(x => x.AnimalName == animalName);
        return GetVirtualCount(animal);
    }
    private int GetVirtualCount(AnimalAutomationStateModel animal)
    {
        int count = animal.VirtualCount;
        var option = GetSelectedOption(animal.AnimalName);
        return GetVirtualCount(option, count);
    }
    private int GetVirtualCount(AnimalProductionOption option, int count)
    {
        int additionals = advancedUpgradeAutomationManager.ExtraVirtualCountBenefit(option.Output.Item);
        return count + additionals;
    }
    public int GetCap(string animalName)
    {
        if (_capByAnimal.TryGetValue(animalName, out int cap))
        {
            var s = _animals.Single(x => x.AnimalName == animalName);
            var option = GetSelectedOption(animalName);
            var virtualCount = GetVirtualCount(option, s.VirtualCount);
            int additional = advancedUpgradeAutomationManager.ExtraQueCountBenefit(option.Output.Item);
            int totals = cap + additional;
            return totals * virtualCount;
        }
        throw new CustomBasicException($"Must have a cap for {animalName}");
    }

    public int GetOutstanding(string animalName)
    {
        var s = _animals.Single(x => x.AnimalName == animalName);
        return s.RequestedTotal - s.DeliveredTowardRequest;
    }

    public int GetRemainingCapacity(string animalName)
    {
        int cap = GetCap(animalName);
        int outstanding = GetOutstanding(animalName);
        int remaining = cap - outstanding;
        return remaining < 0 ? 0 : remaining;
    }

    public TimeSpan? GetTimeUntilNextReady(string animalName)
    {
        if (_init == false)
        {
            return null;
        }

        var s = _animals.Single(x => x.AnimalName == animalName);
        if (s.IsSuppressed || s.Unlocked == false)
        {
            return null;
        }

        int outstanding = s.RequestedTotal - s.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            return null;
        }

        if (s.StoredUnits > 0)
        {
            return TimeSpan.Zero;
        }

        if (s.BlockedAt is not null)
        {
            return null;
        }

        if (s.NextReadyAt is null)
        {
            return null; // no cycle running (should be rare with the new logic)
        }

        var remaining = s.NextReadyAt.Value - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        return remaining;
    }
    public BasicList<AnimalAutomationView> GetUnlockedAnimals
    {
        get
        {
            BasicList<AnimalAutomationView> output = [];
            _animals.ForConditionalItems(x => x.Unlocked && x.IsSuppressed == false, t =>
            {
                AnimalRecipe recipe = _recipes.Single(x => x.Animal == t.AnimalName);
                AnimalAutomationView summary = new()
                {
                    AnimalName = t.AnimalName,
                    ItemProduced = GetSelectedOption(t.AnimalName).Output.Item,
                    StartedAt = t.StartedAt,
                };
                output.Add(summary);
            });
            return output;
        }
    }
    public bool IsBlocked(AnimalAutomationView animal)
    {
        return _animals.Single(x => x.AnimalName == animal.AnimalName).BlockedAt is not null;
    }
    public int VirtualCount(string animalName) => _animals.Single(x => x.AnimalName == animalName).VirtualCount;
    public AnimalProductionOption GetSelectedOption(string animalName)
    {
        var recipe = _recipes.Single(x => x.Animal == animalName);

        if (recipe.Options.Count == 0)
        {
            throw new CustomBasicException($"Animal recipe '{animalName}' has no production options.");
        }

        // Your rule: always choose the first option
        var option = recipe.Options[0];

        // Safety: output must exist
        if (string.IsNullOrWhiteSpace(option.Output.Item) || option.Output.Amount <= 0)
        {
            throw new CustomBasicException($"Animal option output invalid for '{animalName}'.");
        }

        return option;
    }
    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }

        DateTime now = DateTime.Now;

        _animals.ForConditionalItems(x => x.IsSuppressed == false && x.Unlocked == true, a =>
        {
            UpdateTick(a, now);
        });

        await SaveAnimalsAsync();
    }
    private OutputAugmentationSnapshot? GetCurrentAugSnapshotForAnimal(string animalName)
    {
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(animalName);
        return key is null ? null : outputAugmentationManager.GetSnapshot(key);
    }
    private void ApplyAugmentationForCycles_Animals(AnimalAutomationStateModel animal, int producedCycles)
    {
        if (producedCycles <= 0)
        {
            return;
        }

        var snap = GetCurrentAugSnapshotForAnimal(animal.AnimalName);
        if (snap is null)
        {
            return;
        }

        // Animals CAN be double
        if (snap.IsDouble)
        {
            // handled by multiplying StoredUnits production (below),
            // so nothing to store here
            return;
        }

        // Chance extras:
        if (snap.Chance >= 100)
        {
            // If you ever want guaranteed extras for animals you can allow it,
            // but based on your crops rules you probably don't.
            throw new CustomBasicException("Animals: guaranteed extras not supported (Chance >= 100).");
        }

        if (snap.ExtraRewards.Count != 1)
        {
            throw new CustomBasicException("Animals must have exactly one extra reward");
        }
        int virtualCount = GetVirtualCount(animal.AnimalName);
        // same scaling rule as crops: per-cycle per-producer roll
        int rolls = producedCycles * virtualCount;
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
            animal.StoredExtraUnits += hits;
            animal.StoredExtraReward ??= snap.ExtraRewards.Single();

            // simplest safe rule (same comment you had in crops)
            if (!string.Equals(animal.StoredExtraReward, snap.ExtraRewards.Single(), StringComparison.OrdinalIgnoreCase))
            {
                // don't mix reward types mid-run without a dictionary
            }

            _needsSaving = true;
        }
    }

    private bool TryDeliverStoredExactAndWasteRemainderOnComplete_Animals(
        AnimalAutomationStateModel animal,
        AnimalProductionOption option,
        ref int outstanding,
        out bool completedRequest)
    {
        completedRequest = false;

        if (outstanding <= 0)
        {
            animal.StoredUnits = 0;
            animal.StoredExtraUnits = 0;
            animal.StoredExtraReward = null;
            completedRequest = true;
            return false;
        }

        // if nothing stored (including extras), nothing to do
        if (animal.StoredUnits <= 0 && animal.StoredExtraUnits <= 0)
        {
            return false;
        }
        //int virtuals = GetVirtualCount(option, animal.VirtualCount);
        int toDeliver = Math.Min(outstanding, Math.Max(0, animal.StoredUnits));
        int addToDelivery = 0;

        BasicList<ItemAmount> bundle = [];
        
        // Base output (only if we have it)
        if (toDeliver > 0)
        {
            bundle.Add(new(option.Output.Item, toDeliver));
        }

        // Extras (deliver all stored extras; this is what causes blocking when full)
        int extrasToDeliver = 0;
        if (animal.StoredExtraReward is not null && animal.StoredExtraUnits > 0)
        {
            extrasToDeliver = animal.StoredExtraUnits;
            bundle.Add(new(animal.StoredExtraReward, extrasToDeliver));

            // if extra reward is the SAME item as base output, it reduces outstanding sooner
            if (string.Equals(animal.StoredExtraReward, option.Output.Item, StringComparison.OrdinalIgnoreCase))
            {
                addToDelivery = extrasToDeliver;
            }
        }

        if (bundle.Count == 0)
        {
            return false;
        }

        if (inventory.CanAcceptRewards(bundle) == false)
        {
            // 🚫 Can't fit base+extras => block
            return false;
        }

        // deliver base
        if (toDeliver > 0)
        {
            inventory.Add(option.Output.Item, toDeliver);
            animal.StoredUnits -= toDeliver;
            animal.DeliveredTowardRequest += toDeliver;
            outstanding -= (toDeliver - addToDelivery);
        }

        // deliver extras
        if (extrasToDeliver > 0 && animal.StoredExtraReward is not null)
        {
            inventory.Add(animal.StoredExtraReward, extrasToDeliver);
            animal.StoredExtraUnits -= extrasToDeliver;
            if (animal.StoredExtraUnits <= 0)
            {
                animal.StoredExtraUnits = 0;
                animal.StoredExtraReward = null;
            }
        }

        if (outstanding <= 0)
        {
            animal.StoredUnits = 0;
            animal.StoredExtraUnits = 0;
            animal.StoredExtraReward = null;
            completedRequest = true;
        }

        return true;
    }


    // ------------------------- Core Tick Logic -------------------------
    private void UpdateTick(AnimalAutomationStateModel animal, DateTime now)
    {
        if (animal.IsSuppressed || animal.Unlocked == false)
        {
            return;
        }

        var option = GetSelectedOption(animal.AnimalName);

        int outstanding = animal.RequestedTotal - animal.DeliveredTowardRequest;
        if (outstanding <= 0)
        {
            // idle cleanup
            animal.StartedAt = null;
            animal.NextReadyAt = null;
            animal.BlockedAt = null;
            animal.StoredUnits = 0;
            animal.StoredExtraUnits = 0;
            animal.StoredExtraReward = null;
            _needsSaving = true;
            return;
        }

        // 1) First try to deliver anything already produced (base + stored extras).
        // If it can’t fit => block and DO NOT start/complete cycles.
        bool delivered = TryDeliverStoredExactAndWasteRemainderOnComplete_Animals(
            animal, option, ref outstanding, out bool completed);

        if (completed)
        {
            animal.StartedAt = null;
            animal.NextReadyAt = null;
            animal.BlockedAt = null;
            animal.StoredUnits = 0;
            animal.StoredExtraUnits = 0;
            animal.StoredExtraReward = null;
            _needsSaving = true;
            return;
        }

        if ((animal.StoredUnits > 0 || animal.StoredExtraUnits > 0) && delivered == false)
        {
            // Can't deliver produced items because barn/silo full -> blocked.
            animal.BlockedAt = now;
            animal.NextReadyAt = null; // IMPORTANT: don’t let time keep producing
            _needsSaving = true;
            return;
        }

        // 2) If blocked (typically due to feed shortage earlier), see if we can start now.
        if (animal.BlockedAt is not null)
        {
            animal.BlockedAt = null; // we’ll re-block if needed
        }

        // 3) If no cycle is running, try to start ONE cycle by consuming feed now.
        if (animal.NextReadyAt is null)
        {
            TryStartCycleAt(animal, option, now);
            _needsSaving = true;
            return; // either started or blocked due to no feed
        }

        // 4) A cycle is running. If not finished yet, nothing to do.
        if (now < animal.NextReadyAt.Value)
        {
            return;
        }

        // 5) Offline catch-up: complete as many paid cycles as time+feed allow.
        TimeSpan perCycle = GetProductionTimePerCycleAdjusted(option);

        // How many cycle-completions happened based on time?
        // NextReadyAt is the completion time of the CURRENT running cycle.
        // If now == NextReadyAt => 1 cycle complete.
        int cyclesByTime = 1 + (int)Math.Floor((now - animal.NextReadyAt.Value).TotalSeconds / perCycle.TotalSeconds);
        if (cyclesByTime <= 0)
        {
            return;
        }

        // How many cycles can we PAY for with feed right now?
        int feedPerCycle = GetFeedPerCycle(animal, option);
        int cyclesByFeed = int.MaxValue;
        if (feedPerCycle > 0)
        {
            int available = inventory.Get(option.Required);
            cyclesByFeed = available / feedPerCycle;
        }

        // How many cycles do we even need to satisfy outstanding?
        int batchSize = GetBatchSize(animal, option);
        var snap = GetCurrentAugSnapshotForAnimal(animal.AnimalName);
        if (snap?.IsDouble == true)
        {
            batchSize *= 2; // double makes request finish sooner
        }

        int cyclesNeeded = (int)Math.Ceiling(outstanding / (double)batchSize);
        if (cyclesNeeded <= 0)
        {
            cyclesNeeded = 1;
        }

        int cyclesToComplete = Math.Min(cyclesByTime, Math.Min(cyclesByFeed, cyclesNeeded));
        if (cyclesToComplete <= 0)
        {
            // We reached completion time but can’t pay feed for next cycles; stop cycle.
            // NOTE: the currently running cycle was already paid when it started.
            // So if cyclesByFeed == 0, we still should complete exactly 1 (the running one),
            // but cyclesByFeed computed from "available feed" is for ADDITIONAL cycles.
            // To keep this simple: treat cyclesByFeed as additional cycles, not including current.

            cyclesToComplete = 1; // always complete the running paid cycle
        }

        // 6) Complete cyclesToComplete cycles.
        CompleteAnimalCycles(animal, cyclesToComplete, batchSize);

        // Advance NextReadyAt by cycles completed (the cycle schedule continues linearly).
        //animal.NextReadyAt = animal.NextReadyAt.Value.AddSeconds(cyclesToComplete * perCycle.TotalSeconds);
        // We were sitting at the completion time of the *first* completed cycle.
        // The completion time of the *last* completed cycle is:
        //DateTime completedAt =
        //    animal.NextReadyAt.Value.AddSeconds((cyclesToComplete - 1) * perCycle.TotalSeconds);

        // No cycle is running now; we just completed them.
        animal.NextReadyAt = null;
        // 7) After completion, attempt delivery again (may block on space because of extras).
        outstanding = animal.RequestedTotal - animal.DeliveredTowardRequest;
        TryDeliverStoredExactAndWasteRemainderOnComplete_Animals(animal, option, ref outstanding, out completed);

        if (completed)
        {
            animal.StartedAt = null;
            animal.NextReadyAt = null;
            animal.BlockedAt = null;
            animal.StoredUnits = 0;
            animal.StoredExtraUnits = 0;
            animal.StoredExtraReward = null;
            _needsSaving = true;
            return;
        }

        // 8) Start next cycle (pay feed) if still outstanding and not blocked by storage.
        if ((animal.StoredUnits > 0 || animal.StoredExtraUnits > 0)
            && inventory.CanAcceptRewards(BuildCurrentBundlePreview(animal, option)) == false)
        {
            animal.BlockedAt = now;
            animal.NextReadyAt = null;
            _needsSaving = true;
            return;
        }

        TryStartCycleAt(animal, option, animal.NextReadyAt!.Value); // chain start at completion time
        _needsSaving = true;
    }
    private void CompleteAnimalCycles(
        AnimalAutomationStateModel animal,
        int cycles,
        int batchSizeAdjusted)
    {
        if (cycles <= 0)
        {
            return;
        }

        // Produce base
        animal.StoredUnits += cycles * batchSizeAdjusted;

        // Chance extras (same pattern as crops, scaled by VirtualCount)
        ApplyAugmentationForCycles_Animals(animal, cycles);

        _needsSaving = true;
    }
    private static BasicList<ItemAmount> BuildCurrentBundlePreview(AnimalAutomationStateModel animal, AnimalProductionOption option)
    {
        BasicList<ItemAmount> bundle = [];

        int outstanding = animal.RequestedTotal - animal.DeliveredTowardRequest;
        int toDeliver = Math.Min(outstanding, Math.Max(0, animal.StoredUnits));
        if (toDeliver > 0)
        {
            bundle.Add(new(option.Output.Item, toDeliver));
        }

        if (animal.StoredExtraReward is not null && animal.StoredExtraUnits > 0)
        {
            bundle.Add(new(animal.StoredExtraReward, animal.StoredExtraUnits));
        }

        return bundle;
    }
    private int GetBatchSize(AnimalAutomationStateModel animal, AnimalProductionOption option)
    {
        // Items produced per completed cycle across all producers.
        int perProducerYield = option.Output.Amount;
        int virtualCount = GetVirtualCount(option, animal.VirtualCount);
        int batch = virtualCount * perProducerYield;

        if (batch <= 0)
        {
            throw new CustomBasicException($"Invalid batch size for animal '{animal.AnimalName}'. VirtualCount={animal.VirtualCount}, Yield={perProducerYield}");
        }

        return batch;
    }


    private int GetFeedPerCycle(AnimalAutomationStateModel animal, AnimalProductionOption option)
    {
        // If Required is blank, treat as "no feed required"
        if (string.IsNullOrWhiteSpace(option.Required))
        {
            return 0;
        }

        int perProducerInput = option.Input;
        if (perProducerInput < 0)
        {
            throw new CustomBasicException($"Invalid feed input for '{animal.AnimalName}'. Input={perProducerInput}");
        }
        int virtualCount = GetVirtualCount(option, animal.VirtualCount);
        return virtualCount * perProducerInput;
    }

    public string GetDuration(AnimalProductionOption option)
    {
        var time = GetProductionTimePerCycleAdjusted(option);
        return time.GetTimeString;
    }
    private TimeSpan GetProductionTimePerCycleAdjusted(AnimalProductionOption option)
    {
        // Your note: "fastest option (but double)".
        // Since you always choose option[0], treat that as the selected one,
        // and double it to represent automation slower than manual.
        TimeSpan baseDuration = option.Duration;

        // Multiply by authored/global balance multiplier, then apply doubling rule.
        double baseM;
        baseM = balanceManager.Base.AnimalTimeMultiplier;


        TimeSpan reducedBy = timedBoostManager.GetReducedTime(option.Output.Item);
        if (reducedBy < TimeSpan.Zero)
        {
            reducedBy = TimeSpan.Zero;
        }
        baseDuration -= reducedBy;
        if (baseDuration < TimeSpan.Zero)
        {
            baseDuration = TimeSpan.Zero;
        }
        double? speedBonus = advancedUpgradeAutomationManager.GetTimeReductionBenefit(option.Output.Item);
        double bonusM = speedBonus.SpeedBonusToTimeMultiplier(false);
        double m = baseM * bonusM;
        return baseDuration.Apply(m, false);
    }

    private void EnsureRecipeExists(string animalName)
    {
        if (_recipes.Any(x => x.Animal == animalName))
        {
            return;
        }
        throw new CustomBasicException($"Animal recipe not found for '{animalName}'.");
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
            await _profileService.SaveAsync(_animals);
        }
    }
    TimeSpan IAnimalManager.GetTimePerOutputForGivenAnimal(string outputName)
    {
        if (_recipes.Count == 0)
        {
            throw new CustomBasicException("No recipes for animals");
        }
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
    void IAnimalManager.ApplyAnimalProgressionUnlocksFromLevels(BasicList<ItemUnlockRule> rules, BasicList<CatalogOfferModel> offers, int level)
    {
        bool changed = false;
        lock (_lock)
        {
            var offer = offers.FirstOrDefault(x => x.LevelRequired == level);
            if (offer is not null)
            {
                string animalName = offer.TargetName;
                var instance = _animals.First(a => a.AnimalName == animalName && a.Unlocked == false);
                instance.Unlocked = true;
                changed = true;
            }
            if (changed)
            {
                _needsSaving = true;
            }
        }
    }
    AnimalProductionOption IAnimalManager.NextProductionOption(string animal)
    {
        var instance = _animals.First(x => x.AnimalName == animal);
        AnimalProductionOption option = GetSelectedOption(animal);
        //for now, not doing the timed boost manager or output augmentation (later will have this).
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
    void IAnimalManager.PurchaseAnimal(StoreItemRowModel store) => throw new CustomBasicException("Automated animals cannot be purchased in automation mode.");
    int IAnimalManager.GetUnlockedCount(string animalName)
    {
        int count = _animals.Count(x => x.AnimalName == animalName && x.Unlocked && x.IsSuppressed == false);
        if (count == 1)
        {
            return 1;
        }
        if (count > 1)
        {
            throw new CustomBasicException($"Should not have {count} of {animalName}");
        }
        return 0;
    }
}
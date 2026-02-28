namespace FarmSim.Domain.Services.Workshops;
public class WorkshopManager(InventoryManager inventory,
    BalanceManager balanceManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    ) : IScenarioResettable, IWorkshopManager
{
    private IWorkshopRespository _workshopRespository = null!;
    private readonly BasicList<WorkshopInstance> _workshops = [];
    private BasicList<WorkshopRecipe> _recipes = [];
    public event Action? OnWorkshopsUpdated;
    public event Action<string, string>? OnWorkshopItemQued;
    private readonly Lock _lock = new();
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private double _multiplier;
    public event Action<ItemAmount>? OnAugmentedOutput;
    private WorkshopInstance GetWorkshopById(Guid id)
    {
        var workshop = _workshops.SingleOrDefault(t => t.Id == id) ?? throw new CustomBasicException($"Workshop with Id {id} not found.");
        return workshop;
    }
    private WorkshopInstance GetWorkshopById(WorkshopView id) => GetWorkshopById(id.Id);
    public WorkshopRecipeQuestModel GetWorkshopForQuests(string itemCrafting)
    {
        var recipe = _recipes.Single(x => x.Item == itemCrafting);
        return new()
        {
            BuildingName = recipe.BuildingName,
            Duration = recipe.Duration,
            Inputs = new Dictionary<string, int>(recipe.Inputs),
            Item = recipe.Item,
        };
    }
    public BasicList<WorkshopView> GetUnlockedWorkshops
    {
        get
        {
            lock (_lock)
            {
                BasicList<WorkshopView> output = [];

                _workshops.ForConditionalItems(x => x.Unlocked, t =>
                {
                    WorkshopView summary = new()
                    {
                        Id = t.Id,
                        Name = t.BuildingName,
                        SelectedRecipeIndex = t.SelectedRecipeIndex,
                        IsRental = t.IsRental,
                        Unlocked = t.Unlocked,
                        ReadyCount = t.Queue.Count(x => x.State == EnumWorkshopState.ReadyToPickUpManually)
                    };
                    output.Add(summary);
                });


                return output;
            }
        }
    }
    public bool IsInBuilding(string buildingName, string itemToCheck)
    {
        var recipe = _recipes.Single(x => x.Item == itemToCheck);
        return recipe.BuildingName == buildingName;
    }
    public string? GetBuilding(string itemToCheck)
    {
        var recipe = _recipes.Single(x => x.Item == itemToCheck);

        if (_workshops.Any(x => x.BuildingName == recipe.BuildingName && x.Unlocked))
        {
            return null;
        }

        return recipe.BuildingName;

        //if (_workshops.Any(x => x.Unlocked && x.))
        //var workshop = _workshops.FirstOrDefault(x => x.Unlocked == false && )
    }

    //if you purchase, must make sure all proper items are unlocked like it should had (?)
    public bool Unlocked(WorkshopView workshop) => GetWorkshopById(workshop).Unlocked;
    public void CompleteWorkshopJobsBulkUsingPowerGloves(WorkshopView summary)
    {
        var workshop = GetWorkshopById(summary);
        int waiting;
        waiting = workshop.Queue.Count(x => x.State == EnumWorkshopState.Waiting);



        const int minutesPerGlove = 20;
        TimeSpan gloveAmount = TimeSpan.FromMinutes(minutesPerGlove);

        int totalGloves = inventory.Get(CurrencyKeys.PowerGloveWorkshop); // or your glove currency key
        if (totalGloves <= 0)
        {
            return;
        }

        int glovesUsed = 0;

        // Decide which jobs count as "active" (everything not already ready to pick up).
        // This mirrors your other method.
        var activeJobs = workshop.Queue
            .Where(x => x.State != EnumWorkshopState.ReadyToPickUpManually)
            .ToBasicList();

        foreach (var craft in activeJobs)
        {
            // You need a way to compute "time remaining" for this craft.
            // Replace GetTimeRemaining(craft) with whatever your model uses.
            //TimeSpan remaining = GetTimeRemaining(craft);
            TimeSpan remaining;
            if (craft.State == EnumWorkshopState.Active)
            {
                remaining = craft.ReadyTime!.Value;
            }
            else
            {
                remaining = craft.DurationForProcessing;
            }
                

            // If somehow already complete/ready, skip.
            if (remaining <= TimeSpan.Zero)
            {
                continue;
            }

            // Gloves needed = ceiling(remaining / 20 minutes), minimum 1.
            int needed = (int)Math.Ceiling(remaining.TotalMinutes / minutesPerGlove);
            if (needed < 1)
            {
                needed = 1;
            }

            // If we can't fully complete this craft, stop (no partial usage).
            if (glovesUsed + needed > totalGloves)
            {
                break;
            }

            // We have enough gloves -> complete immediately (your existing logic).
            CompleteCraftImmediately(craft);
            glovesUsed += needed;
        }

        if (glovesUsed > 0)
        {
            inventory.Consume(CurrencyKeys.PowerGloveWorkshop, glovesUsed); // or glove currency key
        }


    }

    public void CompleteWorkshopJobsBulkUsingFinishSingle(WorkshopView summary)
    {
        var worshop = GetWorkshopById(summary);

        int active;
        int total;
        active = worshop.Queue.Count(x => x.State != EnumWorkshopState.ReadyToPickUpManually);

        total = inventory.Get(CurrencyKeys.FinishSingleWorkshop);
        int toDo;
        if (total < active)
        {
            toDo = total;
        }
        else
        {
            toDo = active;
        }
        if (toDo == active)
        {
            CompleteAllJobsImmediately(worshop); //if you have enough for all, can show instant.  otherwise, has to manually wait a few seconds.
        }
        else
        {
            toDo.Times(x =>
            {
                var craft = worshop.Queue[x - 1];
                CompleteCraftImmediately(craft);
            });
        }

        inventory.Consume(CurrencyKeys.FinishSingleWorkshop, toDo);
    }

    public void CompleteSingleActiveJobImmediately(WorkshopView workshop)
    {
        if (inventory.Has(CurrencyKeys.FinishSingleWorkshop, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish single workshop consumables left.  Should had called inventory.Has function");
        }
        CompleteActiveJobImmediately(workshop);
        inventory.Consume(CurrencyKeys.FinishSingleWorkshop, 1);
    }

    public void CompleteAllJobsImmediately()
    {
        if (inventory.Has(CurrencyKeys.FinishAllWorkshops, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish all workshop consumables left.");
        }
        foreach (var item in _workshops)
        {
            CompleteAllJobsImmediately(item);
        }
        inventory.Consume(CurrencyKeys.FinishAllWorkshops, 1);
    }


    private void CompleteAllJobsImmediately(WorkshopInstance workshop)
    {
        lock (_lock)
        {
            if (workshop.Queue.Count == 0)
            {
                return;
            }

            var now = DateTime.Now;

            // CASE 1: Manual collection policy -> mark EVERYTHING ready, do not remove anything.
            if (advancedSettingsManager.Settings.Crafting.WorkshopCollectionPolicy != EnumWorkshopCollectionCategories.FullyAutomated)
            {
                foreach (var job in workshop.Queue)
                {
                    job.ReadyForManualPickup();
                }

                _needsSaving = true;
                NotifyWorkshopsUpdated();
                return;
            }

            // CASE 2: Automatic collection policy -> attempt to collect everything in-order.
            // Stop as soon as inventory can’t accept the next output.
            while (workshop.Queue.Any(x => x.State != EnumWorkshopState.ReadyToPickUpManually))
            {
                // Ensure we have an "active" job (for consistency with your model),
                // but we will still finish + collect instantly.
                var active = workshop.Queue.FirstOrDefault(j => j.State == EnumWorkshopState.Active);
                if (active == null)
                {
                    var next = workshop.Queue.FirstOrDefault(j => j.State == EnumWorkshopState.Waiting);
                    if (next == null)
                    {
                        return;
                    }
                    next.Start();
                    active = next;
                    _needsSaving = true;
                }

                // Ensure augmentation promise exists if applicable (so we can roll correctly)
                if (active.OutputPromise is null)
                {
                    bool promised = active.RunPossibleAugmentation();
                    if (promised)
                    {
                        _needsSaving = true;
                    }
                }

                // If we can't add, STOP immediately.
                // (You can optionally flip to manual-ready here so UI explains why it stopped.)
                if (CanAddToInventory(active, workshop.MaxBenefits) == false)
                {
                    // Optional: make it obvious to the player why it halted.
                    active.ReadyForManualPickup();
                    _needsSaving = true;
                    NotifyWorkshopsUpdated();
                    continue;
                    //return; //if they don't have enough for all of it, can go to waste.
                }

                // Add output, respecting augmentation rules
                if (active.OutputPromise is null)
                {
                    inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount);
                }
                else
                {
                    if (rs1.RollHit(active.OutputPromise.Chance))
                    {
                        inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount + 1);
                        OnAugmentedOutput?.Invoke(active.Recipe.Output);
                    }
                    else
                    {
                        inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount);
                    }
                }

                // Complete + remove, then loop for next job
                active.Complete();
                workshop.Queue.RemoveSpecificItem(active);
                _needsSaving = true;
            }

            NotifyWorkshopsUpdated();
        }
    }


    private void CompleteActiveJobImmediately(WorkshopView summary)
    {
        lock (_lock)
        {
            var workshop = GetWorkshopById(summary);

            var active = workshop.Queue.FirstOrDefault(x => x.State == EnumWorkshopState.Active);
            CompleteCraftImmediately(active);



        }
    }
    private static void CompleteCraftImmediately(CraftingJobInstance? active)
    {
        if (active is null)
        {
            return;
        }

        var now = DateTime.Now;

        // Push StartedAt back so that next tick sees elapsed >= DurationForProcessing
        // Add a tiny buffer so you don't lose to timing/precision.
        var buffer = TimeSpan.FromMilliseconds(50);
        active.UpdateStartedAt(now - active.DurationForProcessing - buffer);
    }


    private void ApplyPowerGloveToActiveJob(WorkshopView summary, int used, TimeSpan reduceByPerUse)
    {
        if (used <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var workshop = GetWorkshopById(summary);

            // Only makes sense on an ACTIVE job (matches your earlier design: button only when active)
            var active = workshop.Queue.FirstOrDefault(x => x.State == EnumWorkshopState.Active);
            if (active is null || active.StartedAt is null)
            {
                return;
            }

            TimeSpan totalReduce = reduceByPerUse * used;

            // Shift the start earlier => increases elapsed => finishes sooner
            active.UpdateStartedAt(active.StartedAt.Value - totalReduce);

            _needsSaving = true;
        }

        //no need for the other (because the next second, should be updated).
    }

    public void UsePowerGlove(WorkshopView workshop, int howMany)
    {
        if (inventory.Has(CurrencyKeys.PowerGloveWorkshop, howMany) == false)
        {
            throw new CustomBasicException("Don't have enough power gloves.  Should had called the inventorymanager.Has function");
        }
        ApplyPowerGloveToActiveJob(workshop, howMany, PowerGloveRegistry.ReduceBy);
        inventory.Consume(CurrencyKeys.PowerGloveWorkshop, howMany);
    }

    public Guid StartRental(StoreItemRowModel rental)
    {
        if (rental.Category != EnumCatalogCategory.Workshop)
        {
            throw new CustomBasicException("Only workshops can be rented");
        }
        var instance = _workshops.LastOrDefault(x => x.BuildingName == rental.TargetName && x.Unlocked == false)
            ?? throw new CustomBasicException("No locked building available to rent");
        instance.Unlocked = true;
        instance.IsRental = true; //so later can lock the proper one.  also ui can show the details for it as well.
        _needsSaving = true;
        return instance.Id;
    }

    public bool CanDeleteRental(Guid id)
    {
        WorkshopInstance instance = _workshops.Single(x => x.Id == id);
        if (instance.Unlocked == false)
        {
            return true;
        }
        if (instance.Queue.Count == 0)
        {
            instance.Unlocked = false;
            _needsSaving = true;
            NotifyWorkshopsUpdated();
            return false; //try again in a second.
        }
        if (instance.IsRental == false)
        {
            instance.IsRental = true; //implies its a rental.
            NotifyWorkshopsUpdated();
            _needsSaving = true;
        }
        return false;
    }

    public void DoubleCheckActiveRental(Guid id)
    {
        WorkshopInstance instance = _workshops.Single(x => x.Id == id);
        if (instance.IsRental == false)
        {
            instance.IsRental = true;
            NotifyWorkshopsUpdated();
            _needsSaving = true;
        }
        if (instance.Unlocked == false)
        {
            instance.Unlocked = true;
            NotifyWorkshopsUpdated();
            _needsSaving = true;
        }
    }
    public void UnlockWorkshopPaidFor(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Workshop)
        {
            throw new CustomBasicException("Only workshops can be paid for");
        }
        var item = _workshops.First(x => x.Unlocked == false && x.BuildingName == store.TargetName && x.IsRental == false);
        item.Unlocked = true;
        _needsSaving = true;
    }
    public void ApplyWorkshopProgressionOnLevelUnlocks(BasicList<ItemUnlockRule> rules, BasicList<CatalogOfferModel> offers, int level)
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
                var fins = item.SupportedItems.Single(x => x.Name == craftedItem.ItemName);
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

    public int GetCapcity(WorkshopView summary)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        return workshop.Capacity;
    }
    public int GetLevel(WorkshopView summary)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        return workshop.Level;
    }

    public void UpgradeWorshopLevel(WorkshopView summary, double extraSpeed, bool maxBenefits, double? dropRate)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        workshop.Level++;
        workshop.MaxBenefits = maxBenefits;
        workshop.AdvancedSpeedBonus = extraSpeed;
        workshop.MaxDropRate = dropRate;
        _needsSaving = true;
    }
    public void UpdateCapacity(WorkshopView summary, int capacity)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        workshop.Capacity = capacity;
        _needsSaving = true;
    }
    public CraftingSummary? GetSingleCraftedItem(WorkshopView summary, int index)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);
            //was one based.
            int reals = index - 1;
            if (reals < 0)
            {
                return null;
            }
            try
            {
                CraftingJobInstance job = workshop.Queue[reals];
                CraftingSummary craftSummary = new()
                {
                    Id = job.Id,
                    Name = job.Recipe.Item,
                    State = job.State,
                    ReadyTime = job.State == EnumWorkshopState.Waiting
                            ? "Waiting"
                            : job.ReadyTime?.GetTimeCompact!
                };
                return craftSummary;
            }
            catch (Exception)
            {

                return null;
            }
        }
    }

    public void StartCraftingJob(WorkshopView summary, string item)
    {
        lock (_lock)
        {
            if (CanCraft(summary, item) == false)
            {
                throw new CustomBasicException("Unable to craft.  Should had ran the CanCraft first");
            }
            WorkshopRecipe recipe = _recipes.Single(x => x.Item == item);
            inventory.Consume(recipe.Inputs);
            TimeSpan reduced = timedBoostManager.GetReducedTime(summary.Name);
            WorkshopInstance workshop = GetWorkshopById(summary);
            CraftingJobInstance job = new(recipe, _multiplier, reduced, workshop.AdvancedSpeedBonus, timedBoostManager, outputAugmentationManager);

            workshop.ReducedBy = reduced; //i think this too.
            OnWorkshopItemQued?.Invoke(workshop.BuildingName, recipe.Item);
            workshop.Queue.Add(job);
            _needsSaving = true;
        }
    }
    public WorkshopView? SearchForWorkshop(string searchFor)
    {
        // Find the recipe that produces the desired item
        WorkshopRecipe? target = _recipes.FirstOrDefault(x => x.Item == searchFor); //you may have more than one.   if more than one, has to choose the first one.  you are on your own from here.
        if (target is null)
        {
            return null;
        }

        // Find the workshop instance that owns that recipe
        WorkshopInstance t = _workshops.First(x => x.BuildingName == target.BuildingName); //has to be first now because you can have more than one workshop with the same name.

        // IMPORTANT: compute the index inside that workshop's recipe list
        // Use the SAME ordering concept your UI relies on.
        var workshopRecipes = _recipes
            .Where(r => r.BuildingName == t.BuildingName)
            .ToList();

        int index = workshopRecipes.FindIndex(r => r.Item == searchFor);
        if (index >= 0)
        {
            // Persist selection so when UI loads it already matches
            t.SelectedRecipeIndex = index;
        }



        return new WorkshopView
        {
            Id = t.Id,
            Name = t.BuildingName,
            SelectedRecipeIndex = index,
            ReadyCount = t.Queue.Count(x => x.State == EnumWorkshopState.ReadyToPickUpManually)
        };
    }
    public void UpdateSelectedRecipe(WorkshopView id, int selectedIndex)
    {
        var workshop = GetWorkshopById(id);
        workshop.SelectedRecipeIndex = selectedIndex;
        //OnWorkshopsUpdated?.Invoke();
        _needsSaving = true;
    }


    private void NotifyWorkshopsUpdated()
    {
        OnWorkshopsUpdated?.Invoke();
    }

    public BasicList<WorkshopRecipeSummary> GetRecipesForWorkshop(WorkshopView summary)
    {
        // _multiplier should be the CURRENT workshop time multiplier (<= 1.0)
        double m = _multiplier;
        TimeSpan timeReduction = timedBoostManager.GetReducedTime(summary.Name);
        var firstList = _recipes.Where(x => x.BuildingName == summary.Name).ToBasicList();
        BasicList<WorkshopRecipeSummary> output = [];

        var workshop = _workshops.Single(x => x.Id == summary.Id);

        foreach (var item in firstList)
        {

            var nexts = workshop.SupportedItems.Single(x => x.Name == item.Item);

            TimeSpan duration = item.Duration - timeReduction;
            double extras = workshop.AdvancedSpeedBonus.SpeedBonusToTimeMultiplier();
            WorkshopRecipeSummary fins = new()
            {
                Duration = duration.Apply(m * extras),
                Inputs = item.Inputs,
                Output = item.Output,
                Item = item.Item,
                Unlocked = nexts.Unlocked
            };
            output.Add(fins);
        }
        return output;

    }
    public bool AnyInQueue(WorkshopView summary)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);
            return workshop.Queue.Count != 0;
        }
    }
    public BasicList<CraftingSummary> GetItemsBeingCrafted(WorkshopView summary)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);
            BasicList<CraftingSummary> output = [];
            foreach (var job in workshop.Queue)
            {
                string readyTime = job.State switch
                {
                    EnumWorkshopState.ReadyToPickUpManually => "Ready",
                    EnumWorkshopState.Waiting => "Waiting",
                    EnumWorkshopState.Active => job.ReadyTime?.GetTimeString ?? "",
                    _ => ""
                };

                output.Add(new CraftingSummary
                {
                    Id = job.Id,
                    Name = job.Recipe.Item,
                    State = job.State,
                    ReadyTime = readyTime
                });
            }
            return output;
        }
    }
    public bool CanCraft(WorkshopView summary, string item)
    {
        lock (_lock)
        {
            WorkshopRecipe recipe = _recipes.Single(x => x.Item == item);
            if (recipe.BuildingName != summary.Name)
            {
                return false;
            }
            WorkshopInstance workshop = GetWorkshopById(summary);
            if (workshop.CanAccept(recipe) == false)
            {
                return false;
            }
            return inventory.Has(recipe.Inputs);
        }
    }

    /// <summary>
    /// Queues as many of <paramref name="item"/> as possible across ALL unlocked workshops
    /// with the same BuildingName as <paramref name="source"/>.
    /// Also syncs each workshop's SelectedRecipeIndex to <paramref name="selectedRecipeIndex"/>.
    /// Returns the total number of jobs queued.
    /// </summary>
    public int CraftBulkAcrossSameWorkshops(WorkshopView source, string item, int selectedRecipeIndex)
    {
        // Snapshot targets under lock + sync selection indices.
        BasicList<WorkshopView> targets;
        lock (_lock)
        {
            var matching = _workshops
                .Where(x => x.Unlocked && x.BuildingName == source.Name)
                .ToBasicList();

            // Keep every matching workshop showing the same recipe index the player chose.
            foreach (var w in matching)
            {
                w.SelectedRecipeIndex = selectedRecipeIndex;
            }

            // Create lightweight views for StartCraftingJob/CanCraft (they re-lock internally).
            targets = matching.Select(w => new WorkshopView
            {
                Id = w.Id,
                Name = w.BuildingName,
                SelectedRecipeIndex = selectedRecipeIndex,
                Unlocked = true,
                IsRental = w.IsRental,
                ReadyCount = w.Queue.Count(x => x.State == EnumWorkshopState.ReadyToPickUpManually)
            }).ToBasicList();

            _needsSaving = true;
        }

        int totalQueued = 0;

        // Craft outside the outer lock (StartCraftingJob/CanCraft lock internally).
        foreach (var workshop in targets)
        {
            while (CanCraft(workshop, item))
            {
                StartCraftingJob(workshop, item);
                totalQueued++;
            }
        }

        NotifyWorkshopsUpdated();

        return totalQueued;
    }

    public bool CanPickupManually(WorkshopView summary)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);
            return workshop.Queue.Any(x => x.State == EnumWorkshopState.ReadyToPickUpManually);
        }
    }
    public bool CanAddToInventory(WorkshopView summary)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        return CanAddToInventory(workshop);
    }

    public bool CanAddAllToInventory(WorkshopView summary)
    {
        WorkshopInstance workshop = GetWorkshopById(summary);
        return CanAddToInventory(workshop);
    }
    private bool CanAddAllToInventory(WorkshopInstance workshop)
    {
        var list = workshop.Queue.Where(x => x.State == EnumWorkshopState.ReadyToPickUpManually).ToBasicList();
        if (list.Count == 0)
        {
            return false; //none.
        }
        foreach (var item in list)
        {
            if (CanAddToInventory(item, workshop.MaxBenefits) == false)
            {
                return false;
            }
        }
        return true;
    }

    private bool CanAddToInventory(WorkshopInstance workshop)
    {
        CraftingJobInstance active = workshop.Queue.First(x => x.State == EnumWorkshopState.ReadyToPickUpManually);
        return CanAddToInventory(active, workshop.MaxBenefits);
        //return inventory.CanAdd(active.Recipe.Output);
    }
    private bool CanAddToInventory(CraftingJobInstance active, bool maxBenefits)
    {

        if (active.OutputPromise is null && maxBenefits == false)
        {
            return inventory.CanAdd(active.Recipe.Output);
        }
        int extras;
        if (active.OutputPromise is not null && maxBenefits)
        {
            extras = 2;
        }
        else
        {
            extras = 1;
        }
        return inventory.CanAdd(active.Recipe.Output.Item, active.Recipe.Output.Amount + extras);
    }
    public void PickUpRemaining(WorkshopView summary)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);
            if (CanAddAllToInventory(workshop) == false)
            {
                throw new CustomBasicException("Should had used the CanAddAllToInventory because you were over the barn limits");
            }
            var list = workshop.Queue.Where(x => x.State == EnumWorkshopState.ReadyToPickUpManually).ToBasicList();
            foreach (var item in list)
            {
                PickupManually(workshop, item);
            }
        }
    }
    private void PickupManually(WorkshopInstance workshop, CraftingJobInstance active)
    {
        workshop.Queue.RemoveSpecificItem(active);

        int batches = 1;

        // 1) Existing promise chance (your "augmented output" system)
        bool promiseHit = active.OutputPromise is not null
            && rs1.RollHit(active.OutputPromise.Chance);

        if (promiseHit)
        {
            batches += 1;
            OnAugmentedOutput?.Invoke(active.Recipe.Output);
        }

        // 2) Maxed workshop bonus chance (independent extra)
        bool maxBonusHit = workshop.MaxBenefits
            && rs1.RollHit(workshop.MaxDropRate!.Value);

        if (maxBonusHit)
        {
            batches += 1;
            OnAugmentedOutput?.Invoke(active.Recipe.Output);
        }

        // Award total
        int totalAmount = active.Recipe.Output.Amount * batches;
        inventory.Add(active.Recipe.Output.Item, totalAmount);

        _needsSaving = true;
        NotifyWorkshopsUpdated();
    }
    public void PickupManually(WorkshopView summary)
    {
        lock (_lock)
        {
            WorkshopInstance workshop = GetWorkshopById(summary);

            if (CanAddToInventory(workshop) == false)
            {
                throw new CustomBasicException("Should had used the CanAddToInventory because you were over the barn limits");
            }

            CraftingJobInstance active = workshop.Queue.First(x => x.State == EnumWorkshopState.ReadyToPickUpManually);
            PickupManually(workshop, active);

        }
    }
    public async Task SetStyleContextAsync(WorkshopServicesContext context)
    {
        if (_workshopRespository != null)
        {
            throw new InvalidOperationException("Repository Already set");
        }
        _multiplier = balanceManager.Base.WorkshopTimeMultiplier;
        _workshopRespository = context.WorkshopRespository;
        _recipes = await context.WorkshopRecipes.GetWorkshopRecipesAsync();
        foreach (var item in _recipes)
        {
            itemRegistry.Register(new(item.Output.Item, EnumInventoryStorageCategory.Barn, EnumInventoryItemCategory.Workshops));
        }
        var ours = await context.WorkshopRespository.LoadAsync();
        _workshops.Clear();
        foreach (var item in ours)
        {
            WorkshopInstance workshop = new()
            {
                BuildingName = item.Name
            };
            workshop.Load(item, _recipes, _multiplier, timedBoostManager, outputAugmentationManager);
            _workshops.Add(workshop);
        }
    }
    public async Task UpdateTickAsync()
    {
        _workshops.ForConditionalItems(x => x.SupportedItems.Any(x => x.Unlocked), ProcessBuilding);
        await SaveWorkshopsAsync();
    }
    private void ProcessBuilding(WorkshopInstance workshop)
    {

        // Find active job or start one
        var active = workshop.Queue.FirstOrDefault(j => j.State == EnumWorkshopState.Active);
        if (active == null)
        {
            var next = workshop.Queue.FirstOrDefault(j => j.State == EnumWorkshopState.Waiting);
            if (next != null)
            {
                next.Start();
                _needsSaving = true;
            }
            active = next;
        }

        if (active is null)
        {
            return;
        }
        var now = DateTime.Now;
        var elapsed = now - active.StartedAt!.Value;
        bool rets = active.RunPossibleAugmentation(); //i think if there is something active, must show here.
        if (rets)
        {
            _needsSaving = true;
        }
        while (active != null && elapsed >= active.DurationForProcessing)
        {
            // Consume time for this job
            elapsed -= active.DurationForProcessing;
            if (advancedSettingsManager.Settings.Crafting.WorkshopCollectionPolicy == EnumWorkshopCollectionCategories.FullyAutomated)
            {
                if (CanAddToInventory(active, workshop.MaxBenefits))
                {
                    //if you are doing automatically, no toasts (no toasts should be here).

                    if (active.OutputPromise is null)
                    {
                        inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount);

                    }
                    else
                    {
                        if (rs1.RollHit(active.OutputPromise.Chance))
                        {
                            //because of augmentation, we add one more and you were successful.
                            inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount + 1);
                        }
                        else
                        {
                            inventory.Add(active.Recipe.Output.Item, active.Recipe.Output.Amount);
                        }
                    }
                    active.Complete();
                    workshop.Queue.RemoveSpecificItem(active);

                    _needsSaving = true;
                }
            }
            else
            {
                active.ReadyForManualPickup();
                NotifyWorkshopsUpdated();
                _needsSaving = true;
                return; // stop processing until player picks up
            }

            // Start next job immediately
            var next = workshop.Queue.FirstOrDefault(j => j.State == EnumWorkshopState.Waiting);
            if (next == null)
            {
                return;
            }

            next.Start();
            active = next;
            active.UpdateStartedAt(now - elapsed);
            _needsSaving = true;
        }
    }
    private async Task SaveWorkshopsAsync()
    {
        bool doSave = false;

        lock (_lock)
        {
            if (_needsSaving && DateTime.Now - _lastSave > TimingRules.SaveThrottle)
            {
                _needsSaving = false;
                _lastSave = DateTime.Now;
                doSave = true;
            }
        }

        if (doSave == false)
        {
            return;
        }
        BasicList<WorkshopAutoResumeModel> models;
        lock (_lock)
        {
            models = _workshops
                .Select(w => w.GetWorkshopForSaving)
                .ToBasicList();
        }

        await _workshopRespository.SaveAsync(models);
    }

    void IScenarioResettable.ResetForScenario()
    {
        lock (_lock)
        {
            foreach (var w in _workshops)
            {
                w.Queue.Clear(); //hopefully this simple (?)
            }
            _needsSaving = true;
        }
    }

    int IWorkshopManager.GetUnlockedCount(string buildingName)
    {
        return _workshops.Count(x => x.Unlocked && x.BuildingName == buildingName);
    }

    void IWorkshopManager.PurchaseWorkshop(StoreItemRowModel store)
    {
        UnlockWorkshopPaidFor(store);
    }
}
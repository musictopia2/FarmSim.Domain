namespace FarmSim.Domain.Services.Worksites;
public class WorksiteManager(
    InventoryManager inventory,
    BalanceManager balanceManager,
    ItemRegistry itemRegistry,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager,
    AdvancedSettingsManager advancedSettingsManager
    ) : IScenarioResettable, IWorksiteManager
{
    private IWorksiteRepository _worksiteRepository = null!;
    private IWorkerRepository _workerRepository = null!;
    private readonly BasicList<WorksiteInstance> _worksites = [];
    private BasicList<UnlockModel> _workerStates = [];
    private BasicList<WorkerRecipe> _allWorkers = [];
    private bool _needsSaving;
    private DateTime _lastSave = DateTime.MinValue;
    private readonly Lock _lock = new();

    public event Action<string, ItemAmount>? OnRewardPickedUp;
    public TimeSpan GetTimeForWorksiteItem(string itemName)
    {
        foreach (var worksite in _worksites)
        {
            if (worksite.HasRecipe(itemName))
            {
                return worksite.BaseDuration;
            }
        }
        throw new CustomBasicException($"Non item found for {itemName}");
    }
    public BasicList<string> GetAllUnlockedWorkers()
    {
        return _workerStates.Where(x => x.Unlocked).Select(x => x.Name).ToBasicList();
    }
    public WorkerRecipe GetWorkerDetails(string worker)
    {
        return _allWorkers.Single(x => x.WorkerName == worker);
    }
    public BasicList<WorkerRecipe> GetUnlockedWorkers(string location)
    {
        var unlockedNames = _workerStates.Where(x => x.Unlocked).Select(x => x.Name).ToBasicList();
        BasicList<WorkerRecipe> output = [];

        foreach (var unlockedName in unlockedNames)
        {
            var recipe = _allWorkers.Single(x => x.WorkerName == unlockedName);
            if (recipe.WorkerStatus == EnumWorkerStatus.Selected)
            {
                output.Add(recipe);
            }
            else if (recipe.WorkerStatus == EnumWorkerStatus.None)
            {
                output.Add(recipe);
            }
            else if (recipe.CurrentLocation == location)
            {
                output.Add(recipe);
            }
        }
        return output;
    }

    public void CompleteSingleWorksiteImmediately(string location)
    {
        if (inventory.Has(CurrencyKeys.FinishSingleWorksite, 1) == false)
        {
            throw new CustomBasicException("You do not have any finish single worksite consumables left.  Should had called inventory.Has function");
        }
        CompleteActiveJobImmediately(location);
        inventory.Consume(CurrencyKeys.FinishSingleWorksite, 1);
    }
    public void CompleteAllJobsImmediately()
    {
        if (inventory.Has(CurrencyKeys.FinishAllWorksites, 1) == false)
        {
            throw new CustomBasicException("You do not have any Finish All Worksites consumabes left.  Should had called inventory.Has function");
        }
        foreach (var item in _worksites)
        {
            CompleteActiveJobImmediately(item.Location);
        }
        inventory.Consume(CurrencyKeys.FinishAllWorksites, 1);
    }

    private void CompleteActiveJobImmediately(string location)
    {
        lock (_lock)
        {
            WorksiteInstance instance = GetWorksiteByLocation(location);


            // Reduce by the remaining time + a tiny buffer.
            // Easiest way: reduce by the full effective duration (always enough).
            var buffer = TimeSpan.FromMilliseconds(50);

            instance.ApplyTimeReduction(instance.EffectiveDuration + buffer);
            _needsSaving = true;
        }
    }
    private void ApplyPowerGloveToWorksite(string location, int used, TimeSpan reduceByPerUse)
    {
        if (used <= 0)
        {
            return;
        }

        TimeSpan totalReduce = reduceByPerUse * used;

        lock (_lock)
        {
            WorksiteInstance instance = GetWorksiteByLocation(location);
            instance.ApplyTimeReduction(totalReduce);
            _needsSaving = true;
        }
    }
    public void UsePowerGlove(string location, int howMany)
    {
        if (inventory.Has(CurrencyKeys.PowerGloveWorksite, howMany) == false)
        {
            throw new CustomBasicException("Don't have enough power gloves.  Should had called the inventorymanager.Has function");
        }
        ApplyPowerGloveToWorksite(location, howMany, PowerGloveRegistry.ReduceBy);
        inventory.Consume(CurrencyKeys.PowerGloveWorksite, howMany);
    }
    public async Task UnlockWorkerAcquiredAsync(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Worker)
        {
            throw new CustomBasicException("Only workers can be acquired");
        }
        var item = _workerStates.Single(x => x.Name == store.TargetName && x.Unlocked == false);
        item.Unlocked = true;
        await _workerRepository.SaveAsync(_workerStates);
    }
    public async Task<bool> CanDeleteWorkerRentalAsync(RentalInstanceModel rental)
    {
        if (rental.Category != EnumCatalogCategory.Worker)
        {
            throw new CustomBasicException("Only workers can possibly delete the rental");
        }
        var item = _workerStates.Single(x => x.Name == rental.TargetName);
        if (item.Unlocked == false)
        {
            return true;
        }
        var instance = _allWorkers.Single(x => x.WorkerName == rental.TargetName);
        if (instance.WorkerStatus == EnumWorkerStatus.Working)
        {
            return false;
        }
        item.Unlocked = false;
        await _workerRepository.SaveAsync(_workerStates);
        return false; //unlock but wait until next cycle to delete
    }
    public async Task DoubleCheckActiveWorkerRentalAsync(RentalInstanceModel rental)
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
    public void UnlockWorksitePaidFor(StoreItemRowModel store)
    {
        if (store.Category != EnumCatalogCategory.Worksite)
        {
            throw new CustomBasicException("Only worksites can be paid for");
        }
        var item = _worksites.Single(x => x.Unlocked == false && x.Location == store.TargetName);
        item.Unlocked = true;
        _needsSaving = true;
    }

    public async Task ApplyWorkerProgressionUnlocksFromLevelsAsync(BasicList<CatalogOfferModel> offers, int level)
    {
        //only unlock current level.
        var item = offers.SingleOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }

        var worker = _workerStates.Single(x => x.Name == item.TargetName);
        worker.Unlocked = true;
        await _workerRepository.SaveAsync(_workerStates);

    }

    public int TotalWorkersAllowed(string location)
    {
        var site = GetWorksiteByLocation(location);
        return site.MaximumWorkers;
    }
    public string? GetPossibleWorksiteForItem(string name) => _worksites.SingleOrDefault(x => x.HasRecipe(name))?.Location;
    public void ApplyWorksiteProgressionUnlocksFromLevels(BasicList<CatalogOfferModel> offers, int level)
    {
        //only unlock current level.
        var item = offers.SingleOrDefault(x => x.LevelRequired == level);
        if (item is null)
        {
            return;
        }
        var instance = _worksites.Single(x => x.Location == item.TargetName);
        instance.Unlocked = true;
        _needsSaving = true;
    }


    private WorksiteInstance GetWorksiteByLocation(string location)
    {
        var worksite = _worksites.SingleOrDefault(t => t.Location == location) ?? throw new CustomBasicException($"Worksite with location {location} not found.");
        return worksite;
    }

    public void AddWorker(string location, WorkerRecipe worker)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        instance.AddWorker(worker);
        _needsSaving = true;
    }
    public void RemoveWorker(string location, WorkerRecipe worker)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        instance.RemoveWorker(worker);
        _needsSaving = true;
    }


    public bool CanResetToFocused(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.CanResetToFocused;
    }
    public void ResetToFocused(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        instance.ResetToFocused();
        _needsSaving = true;
    }
    public bool CanStartJob(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.CanStartJob(inventory);
    }
    public bool NeedsAutomateError(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.NeedsAutomateError();
    }
    public void StartJob(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        instance.StartJob(inventory);
        _needsSaving = true;
    }
    public bool CanCollectRewards(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        //later can have other reasons you cannot collect rewards (later).
        return instance.CanCollectRewards;
    }
    public bool CanCollectRewardsWithLimits(string location)
    {
        var list = GetRewards(location);
        return inventory.CanAcceptRewards(list);
    }
    public BasicList<ItemAmount> GetRewards(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.GetRewards();
    }
    public void CollectAllRewards(string location, BasicList<ItemAmount> rewards)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        CollectAllRewards(instance, rewards);
    }

    public void CollectSpecificReward(string location, ItemAmount reward)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        instance.CollectSpecificReward(reward);
        OnRewardPickedUp?.Invoke(location, reward);
        inventory.Add(reward.Item, reward.Amount);
        _needsSaving = true;
    }

    //give a person a choice (so if they had ui that forces in order, can do).
    public void CollectFirstReward(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        ItemAmount reward = instance.GetFirstReward;
        OnRewardPickedUp?.Invoke(instance.Location, reward);
        inventory.Add(reward.Item, reward.Amount);
        _needsSaving = true;
    }

    private void CollectAllRewards(WorksiteInstance instance, BasicList<ItemAmount> rewards)
    {
        rewards.ForEach(reward =>
        {
            OnRewardPickedUp?.Invoke(instance.Location, reward);
            inventory.Add(reward.Item, reward.Amount);
        });
        instance.CollectAllRewards();
        _needsSaving = true;
    }
    public BasicList<WorkerRecipe> GetWorkers(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.Workers;
    }
    public EnumWorksiteState GetStatus(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.Status;
    }
    public string GetDurationText(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return $"{instance.StartText} ({instance.GetPreviewDuration(timedBoostManager).GetTimeString})"; //i think this is what is needed (?)
    }
    public string GetProgressText(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        if (instance.ReadyTime is null)
        {
            return "Finished";
        }
        try
        {
            return $"Come back in {instance.ReadyTime.Value.GetTimeString}";
        }
        catch (Exception)
        {
            return "Finished";
        }

    }
    public BasicList<WorksiteRewardPreview> GetPreview(string location)
    {
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.GetPreview();
    }
    public BasicList<ItemAmount> SuppliesNeeded(string location)
    {
        if (timedBoostManager.HasNoSuppliesNeededForWorksites())
        {
            return [];
        }
        WorksiteInstance instance = GetWorksiteByLocation(location);
        return instance.SuppliesNeeded;
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

    public async Task SetStyleContextAsync(WorksiteServicesContext worksiteContext,
        WorkerServicesContext workerContext
        )
    {
        if (_worksiteRepository != null)
        {
            throw new InvalidOperationException("Persistance Already set");
        }
        _worksiteRepository = worksiteContext.WorksiteRepository;
        _workerRepository = workerContext.WorkerRepository;
        BasicList<WorksiteRecipe> recipes = await worksiteContext.WorksiteRecipes.GetWorksitesAsync();
        foreach (var item in recipes)
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
        _worksites.Clear();
        _workerStates = await workerContext.WorkerRepository.LoadAsync();
        var ours = await worksiteContext.WorksiteRepository.LoadAsync();
        _allWorkers = await workerContext.WorkerRecipes.GetWorkersAsync();
        double offset = balanceManager.Base.WorksiteTimeMultiplier;
        foreach (var item in ours)
        {
            WorksiteRecipe recipe = recipes.Single(x => x.Location == item.Name);
            WorksiteInstance instance = new(recipe, offset, _allWorkers, _workerStates, timedBoostManager, outputAugmentationManager, advancedSettingsManager);
            instance.Load(item, timedBoostManager);
            foreach (var tempWorker in item.Workers)
            {
                WorkerRecipe reals = _allWorkers.Single(x => x.WorkerName == tempWorker.WorkerName);
                reals.WorkerStatus = tempWorker.WorkerStatus;
                reals.CurrentLocation = tempWorker.CurrentLocation;
                instance.AddWorkerAfterLoading(reals);
            }
            _worksites.Add(instance);
        }
    }
    public void StoreRewards(string location, BasicList<ItemAmount> rewards)
    {
        var worksite = _worksites.Single(x => x.Location == location);
        worksite.StoreRewards(rewards);
        _needsSaving = true;
    }

    // Tick method called by game timer
    public async Task UpdateTickAsync()
    {
        _worksites.ForConditionalItems(x => x.Unlocked && x.Status != EnumWorksiteState.None, worksite =>
        {
            worksite.UpdateTick();
            if (worksite.NeedsSaving)
            {
                _needsSaving = true;
            }
            //may be automated now.
            if (worksite.Status == EnumWorksiteState.Collecting && advancedSettingsManager.Settings.AutomateWorksiteCollection == true)
            {
                var rewards = worksite.GetRewards();
                if (inventory.CanAcceptRewards(rewards))
                {
                    CollectAllRewards(worksite, rewards);
                }
            }
        });
        await SaveWorksitesAsync();
    }

    private async Task SaveWorksitesAsync()
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
            BasicList<WorksiteAutoResumeModel> list = _worksites
             .Select(worksite => worksite.GetWorksiteForSaving)
             .ToBasicList();
            await _worksiteRepository.SaveAsync(list);
        }
    }

    void IScenarioResettable.ResetForScenario()
    {
        foreach (var item in _worksites)
        {
            item.Reset();
        }
        _needsSaving = true;
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
}
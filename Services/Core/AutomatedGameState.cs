namespace FarmSim.Domain.Services.Core;
public class AutomatedGameState : IGameTimer
{
    public AutomatedGameState(RulesManager rulesManager,
        InventoryManager inventory,
        IInventoryFactory startFactory,
        ICropAutomationFactory cropFactory,
        ITreeAutomationFactory treeFactory,
        IAnimalAutomationFactory animalFactory,
        IWorkshopAutomationFactory workshopFactory,
        IWorksiteAutomationFactory worksiteFactory,
        IWorkerFactory workerFactory,
        IQuestFactory questFactory,
        IUpgradeFactory upgradeFactory,
        IProgressionFactory progressionFactory,
        ICatalogFactory catalogFactory,
        IStoreFactory storeFactory,
        IItemFactory itemFactory,
        IInstantUnlimitedFactory instantUnlimitedFactory,
        ITimedBoostFactory timedBoostFactory,
        IOutputAugmentationFactory outputAugmentationFactory,
        IRentalFactory rentalFactory,
        IScenarioFactory scenarioFactory,
        IRandomChestFactory randomChestFactory,
        IAchievementFactory achievementFactory,
        IAdvancedSettingsFactory advancedSettingsFactory,
        IAdvancedUpgradesAutomationFactory advancedUpgradesAutomationFactory,
        IGiftFactory giftFactory,
        CropAutomationManager cropManager,
        TreeAutomationManager treeManager, //keep same name is okay.
        AnimalAutomationManager animalManager,
        WorkshopAutomationManager workshopManager,
        WorksiteAutomationManager worksiteManager,
        QuestManager questManager,
        UpgradeManager upgradeManager,
        ProgressionManager progressionManager,
        CatalogManager catalogManager,
        StoreManager storeManager,
        ItemManager itemManager,
        InstantUnlimitedManager instantUnlimitedManager,
        TimedBoostManager timedBoostManager,
        OutputAugmentationManager outputAugmentationManager,
        RentalManager rentalManager,
        ScenarioManager scenarioManager,
        RandomChestManager randomChestManager,
        AchievementManager achievementManager,
        AdvancedSettingsManager advancedSettingsManager,
        BalanceManager balanceManager,
        AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager,
        GiftManager giftManager,
        FarmKey farm,
        EnumRuleCategory ruleCategory)
    {
        _rulesManager = rulesManager;
        _inventory = inventory;
        _startFactory = startFactory;
        _cropFactory = cropFactory;
        _treeFactory = treeFactory;
        _animalFactory = animalFactory;
        _workshopFactory = workshopFactory;
        _worksiteFactory = worksiteFactory;
        _workerFactory = workerFactory;
        _questFactory = questFactory;
        _upgradeFactory = upgradeFactory;
        _progressionFactory = progressionFactory;
        _catalogFactory = catalogFactory;
        _storeFactory = storeFactory;
        _itemFactory = itemFactory;
        _instantUnlimitedFactory = instantUnlimitedFactory;
        _timedBoostFactory = timedBoostFactory;
        _outputAugmentationFactory = outputAugmentationFactory;
        _rentalFactory = rentalFactory;
        _scenarioFactory = scenarioFactory;
        _randomChestFactory = randomChestFactory;
        _achievementFactory = achievementFactory;
        _advancedSettingsFactory = advancedSettingsFactory;
        _advancedUpgradesAutomationFactory = advancedUpgradesAutomationFactory;
        _giftFactory = giftFactory;
        _cropManager = cropManager;
        _treeManager = treeManager;
        _animalManager = animalManager;
        _workshopManager = workshopManager;
        _worksiteManager = worksiteManager;
        _questManager = questManager;
        _upgradeManager = upgradeManager;
        _progressionManager = progressionManager;
        _catalogManager = catalogManager;
        _storeManager = storeManager;
        _itemManager = itemManager;
        _instantUnlimitedManager = instantUnlimitedManager;
        _timedBoostManager = timedBoostManager;
        _outputAugmentationManager = outputAugmentationManager;
        _rentalManager = rentalManager;
        _scenarioManager = scenarioManager;
        _randomChestManager = randomChestManager;
        _achievementManager = achievementManager;
        _advancedSettingsManager = advancedSettingsManager;
        _balanceManager = balanceManager;
        _advancedUpgradeAutomationManager = advancedUpgradeAutomationManager;
        _giftManager = giftManager;
        _farm = farm;
        _ruleCategory = ruleCategory;
        _container = new MainFarmContainer
        {
            InventoryManager = inventory,
            CropAutomationManager = cropManager,
            TreeAutomationManager = treeManager,
            AnimalAutomationManager = animalManager,
            WorkshopAutomationManager = workshopManager,
            WorksiteAutomationManager = worksiteManager,
            QuestManager = questManager,
            UpgradeManager = upgradeManager,
            ProgressionManager = progressionManager,
            CatalogManager = catalogManager,
            StoreManager = storeManager,
            InstantUnlimitedManager = instantUnlimitedManager,
            TimedBoostManager = timedBoostManager,
            ItemManager = itemManager,
            OutputAugmentationManager = outputAugmentationManager,
            RentalManager = rentalManager,
            ScenarioManager = scenarioManager,
            RandomChestManager = randomChestManager,
            AchievementManager = achievementManager,
            AdvancedSettingsManager = advancedSettingsManager,
            RulesManager = rulesManager,
            BalanceManager = balanceManager,
            AdvancedUpgradeAutomationManager = advancedUpgradeAutomationManager,
            GiftManager = giftManager,
            FarmKey = farm
        };
    }
    private readonly RulesManager _rulesManager;
    private readonly InventoryManager _inventory;
    private readonly IInventoryFactory _startFactory;
    private readonly ICropAutomationFactory _cropFactory;
    private readonly ITreeAutomationFactory _treeFactory;
    private readonly IAnimalAutomationFactory _animalFactory;
    private readonly IWorkshopAutomationFactory _workshopFactory;
    private readonly IWorksiteAutomationFactory _worksiteFactory;
    private readonly IWorkerFactory _workerFactory;
    private readonly IQuestFactory _questFactory;
    private readonly IUpgradeFactory _upgradeFactory;
    private readonly IProgressionFactory _progressionFactory;
    private readonly ICatalogFactory _catalogFactory;
    private readonly IStoreFactory _storeFactory;
    private readonly IItemFactory _itemFactory;
    private readonly IInstantUnlimitedFactory _instantUnlimitedFactory;
    private readonly ITimedBoostFactory _timedBoostFactory;
    private readonly IOutputAugmentationFactory _outputAugmentationFactory;
    private readonly IRentalFactory _rentalFactory;
    private readonly IScenarioFactory _scenarioFactory;
    private readonly IRandomChestFactory _randomChestFactory;
    private readonly IAchievementFactory _achievementFactory;
    private readonly IAdvancedSettingsFactory _advancedSettingsFactory;
    private readonly IAdvancedUpgradesAutomationFactory _advancedUpgradesAutomationFactory;
    private readonly IGiftFactory _giftFactory;
    private readonly CropAutomationManager _cropManager;
    private readonly TreeAutomationManager _treeManager;
    private readonly AnimalAutomationManager _animalManager;
    private readonly WorkshopAutomationManager _workshopManager;
    private readonly WorksiteAutomationManager _worksiteManager;
    private readonly QuestManager _questManager;
    private readonly UpgradeManager _upgradeManager;
    private readonly ProgressionManager _progressionManager;
    private readonly CatalogManager _catalogManager;
    private readonly StoreManager _storeManager;
    private readonly ItemManager _itemManager;
    private readonly InstantUnlimitedManager _instantUnlimitedManager;
    private readonly TimedBoostManager _timedBoostManager;
    private readonly OutputAugmentationManager _outputAugmentationManager;
    private readonly RentalManager _rentalManager;
    private readonly ScenarioManager _scenarioManager;
    private readonly RandomChestManager _randomChestManager;
    private readonly AchievementManager _achievementManager;
    private readonly AdvancedSettingsManager _advancedSettingsManager;
    private readonly BalanceManager _balanceManager;
    private readonly AdvancedUpgradeAutomationManager _advancedUpgradeAutomationManager;
    private readonly GiftManager _giftManager;
    private FarmKey _farm;
    private readonly EnumRuleCategory _ruleCategory;
    readonly MainFarmContainer _container;
    FarmKey? IGameTimer.FarmKey => _farm;
    MainFarmContainer IGameTimer.FarmContainer
    {
        get
        {
            return _container;
        }
    }
    private bool _init = false;
    async Task IGameTimer.SetThemeContextAsync(FarmKey farm)
    {
        if (farm.Equals(_farm) == false)
        {
            throw new CustomBasicException("I think must be same farm");
        }
        if (string.IsNullOrWhiteSpace(farm.PlayerName) || string.IsNullOrWhiteSpace(farm.Theme))
        {
            throw new CustomBasicException("Must specify player and farm themes now");
        }
        _farm = farm; //hopefully no problem resetting here (?)
        _rulesManager.SetStyleContext(farm, _ruleCategory);
        //this means anywhere you need it, has it.
        await _balanceManager.SetStyleContextAsync(farm); //for now, okay.  if that changes. rethink.
        CatalogServicesContext catalogContext = _catalogFactory.GetCatalogServices(farm);
        await _catalogManager.SetCatalogStyleContextAsync(catalogContext, farm); //must be loaded first now.
        IInventoryRepository init = _startFactory.GetInventoryServices(farm);
        IInventoryProfile inventoryProfileService = _startFactory.GetInventoryProfile(farm);
        Dictionary<string, int> starts = await init.LoadAsync(farm);
        InventoryStorageProfileModel inventoryStorageProfileModel = await inventoryProfileService.LoadAsync();
        _inventory.LoadStartingInventory(starts, inventoryStorageProfileModel);
        AdvancedSettingsServicesContext settingsContext = _advancedSettingsFactory.GetAdvancedSettingsServices(farm);
        await _advancedSettingsManager.SetAdvancdSettingsStyleContextAsync(settingsContext);
        TimedBoostServicesContext timedBoostContext = _timedBoostFactory.GetTimedBoostServices(farm);
        await _timedBoostManager.SetTimedBoostStyleContextAsync(timedBoostContext);
        OutputAugmentationServicesContext augmentationOutputContext = _outputAugmentationFactory.GetOutputAugmentationServices(farm);
        await _outputAugmentationManager.SetOutputAugmentationStyleContextAsync(augmentationOutputContext, farm);
        CropAutomationServicesContext cropContext = _cropFactory.GetCropServices(farm);
        await _cropManager.SetStyleContextAsync(cropContext, farm);
        TreeAutomationServicesContext treeContext = _treeFactory.GetTreeServices(farm);
        await _treeManager.SetStyleContextAsync(treeContext, farm);
        AnimalAutomationServicesContext animalContext = _animalFactory.GetAnimalServices(farm);
        await _animalManager.SetStyleContextAsync(animalContext, farm);
        WorkshopAutomationServicesContext workshopContext = _workshopFactory.GetWorkshopServices(farm);
        await _workshopManager.SetStyleContextAsync(workshopContext, farm);
        WorksiteAutomationServicesContext worksiteContext = _worksiteFactory.GetWorksiteServices(farm);
        WorkerServicesContext workerContext = _workerFactory.GetWorkerServices(farm);
        await _worksiteManager.SetStyleContextAsync(worksiteContext, workerContext, farm);
        UpgradeServicesContext upgradeContext = _upgradeFactory.GetUpgradeServices(farm);
        await _upgradeManager.SetInventoryStyleContextAsync(upgradeContext, inventoryStorageProfileModel, farm);
        UpgradeAutomationServicesContext advancedContext = _advancedUpgradesAutomationFactory.GetUpgradeServices(farm);
        await _advancedUpgradeAutomationManager.SetStyleContextAsync(advancedContext, farm);
        ProgressionServicesContext progressContext = _progressionFactory.GetProgressionServices(farm);
        await _progressionManager.SetProgressionStyleContextAsync(progressContext, farm);
        StoreServicesContext storeContext = _storeFactory.GetStoreServices(farm);
        await _storeManager.SetProgressionStyleContextAsync(storeContext);
        ItemServicesContext itemContext = _itemFactory.GetItemServices(farm, _cropFactory.GetCropServices(farm).CropRecipes,
            _treeFactory.GetTreeServices(farm).TreeRecipes,
            _animalFactory.GetAnimalServices(farm).AnimalRecipes,
            _workshopFactory.GetWorkshopServices(farm).WorkshopRecipes,
            _worksiteFactory.GetWorksiteServices(farm).WorksiteRecipes,
            _progressionFactory.GetProgressionServices(farm).CropProgressionPlanProvider,
            _catalogFactory.GetCatalogServices(farm).CatalogDataSource,
            _progressionFactory.GetProgressionServices(farm).AnimalProgressionPlanProvider,
            _progressionFactory.GetProgressionServices(farm).WorkshopProgressionPlanProvider
            );
        await _itemManager.SetItemStyleContextAsync(itemContext, farm);
        QuestServicesContext questContext = _questFactory.GetQuestServices(farm, _cropManager, _treeManager, _animalManager, _workshopManager, _itemManager, _rulesManager);
        await _questManager.SetStyleContextAsync(questContext, farm);
        InstantUnlimitedServicesContext instantUnlimitedContext = _instantUnlimitedFactory.GetInstantUnlimitedServices(farm);
        await _instantUnlimitedManager.SetInstantUnlimitedStyleContextAsync(instantUnlimitedContext);
        RentalsServicesContext rentalContext = _rentalFactory.GetRentalServices(farm);
        await _rentalManager.SetRentalStyleContextAsync(rentalContext);
        ScenarioServicesContext scenarioContext = _scenarioFactory.GetScenarioServices(farm, _instantUnlimitedManager,
            _cropManager, _treeManager, _animalManager, _workshopManager, _worksiteManager, _itemManager);
        await _scenarioManager.SetStyleContextAsync(scenarioContext, farm);
        RandomChestServicesContext randomChestContext = _randomChestFactory.GetRandomChestServices(farm, _progressionManager);
        _randomChestManager.SetRandomChestStyleContext(randomChestContext);
        AchievementServicesContext achievementContext = _achievementFactory.GetAchievementServices(farm);
        await _achievementManager.SetAchievementStyleContextAsync(achievementContext, farm);
        GiftServicesContext giftServicesContext = _giftFactory.GetGiftServices();
        await _giftManager.SetStyleContextAsync(giftServicesContext, farm);
        _init = true;
    }
    async Task IGameTimer.TickAsync()
    {
        if (_init == false)
        {
            return;
        }
        await _giftManager.UpdateTickAsync();
        await _treeManager.UpdateTickAsync();
        await _cropManager.UpdateTickAsync();
        await _animalManager.UpdateTickAsync();
        await _workshopManager.UpdateTickAsync();
        await _worksiteManager.UpdateTickAsync();
        await _timedBoostManager.UpdateTickAsync();
        await _rentalManager.UpdateTickAsync();
    }
}
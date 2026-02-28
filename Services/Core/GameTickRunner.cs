namespace FarmSim.Domain.Services.Core;
public class GameTickRunner(IStartFarmRegistry farmRegistry,
    IFarmRulesProvider rulesProvider,
    GameRegistry gameRegistry,
    IInventoryFactory inventoryFactory,
    ICropFactory cropFactory,
    ITreeFactory treeFactory,
    IAnimalFactory animalFactory,
    IWorkshopFactory workshopFactory,
    IWorksiteFactory worksiteFactory,
    IWorkerFactory workerFactory,
    IQuestFactory questFactory,
    IScenarioFactory scenarioFactory,
    IUpgradeFactory upgradeFactory,
    IAdvancedUpgradesAutomationFactory advancedUpgradesAutomationFactory,
    IProgressionFactory progressionFactory,
    ICatalogFactory catalogFactory,
    IStoreFactory storeFactory,
    IItemFactory itemFactory,
    IInstantUnlimitedFactory instantUnlimitedFactory,
    ITimedBoostFactory timedBoostFactory,
    IOutputAugmentationFactory outputAugmentationFactory,
    IRentalFactory rentalFactory,
    IRandomChestFactory randomChestFactory,
    IAchievementFactory achievementFactory,
    IAdvancedSettingsFactory advancedSettingsFactory,
    IBaseBalanceProvider baseBalanceProvider,
    IInventoryRepository inventoryRepository,
    StartingOptionsCoordinatorService startingOptionsCoordinatorService,
    ITreeAutomationFactory treeAutomationFactory,
    ICropAutomationFactory cropAutomationFactory,
    IAnimalAutomationFactory animalAutomationFactory,
    IWorkshopAutomationFactory workshopAutomationFactory,
    IWorksiteAutomationFactory worksiteAutomationFactory,
    IGiftFactory giftFactory,
    GiftLedgerService giftLedgerService
    ) : IGameTickRunner
{
    private readonly IBaseBalanceProvider _baseBalanceProvider = baseBalanceProvider;
    private readonly StartingOptionsCoordinatorService _startingOptionsCoordinatorService = startingOptionsCoordinatorService;

    async Task IGameTickRunner.InitializeAsync(CancellationToken token)
    {
        BasicList<FarmKey> farms = await farmRegistry.GetFarmsAsync();
        foreach (var farm in farms)
        {
            token.ThrowIfCancellationRequested();
            EnumRuleCategory category = await rulesProvider.GetRulesAsync(farm.PlayerName);
            if (category == EnumRuleCategory.NotChosen)
            {

                continue; //pending farm.
            }
            if (ShouldInitializeFarm(farm, category) == false)
            {
                continue; // pretend it does not exist
            }
            await InitializeFarmInternalAsync(farm, category, token);
        }
    }
    private async Task InitializeFarmInternalAsync(FarmKey farm, EnumRuleCategory category, CancellationToken token)
    {
        // NOTE: this is your existing block, unchanged other than living in one place.
        token.ThrowIfCancellationRequested();
        IGameTimer timer;
        if (category == EnumRuleCategory.Manual)
        {
            timer = GetManualTimer(farm, category);
        }
        else
        {
            timer = GetAutomatedTimer(farm, category);
        }
        await gameRegistry.InitializeFarmAsync(timer, farm);
    }
    private IGameTimer GetManualTimer(FarmKey farm, EnumRuleCategory category)
    {
        ItemRegistry itemRegistry = new();
        InventoryManager inventory = new(farm, inventoryRepository, itemRegistry);
        RulesManager rulesManager = new();
        AdvancedSettingsManager advancedSettingsManager = new();
        TimedBoostManager timedBoostManager = new(advancedSettingsManager);
        OutputAugmentationManager outputAugmentationManager = new();
        BalanceManager balanceManager = new(_baseBalanceProvider, rulesManager);
        CropManager cropManager = new(inventory, balanceManager, itemRegistry, timedBoostManager, outputAugmentationManager, advancedSettingsManager);
        TreeManager treeManager = new(inventory, balanceManager, itemRegistry, timedBoostManager, outputAugmentationManager, advancedSettingsManager);
        AnimalManager animalManager = new(inventory, balanceManager, itemRegistry, timedBoostManager, outputAugmentationManager, advancedSettingsManager);
        WorkshopManager workshopManager = new(inventory, balanceManager, itemRegistry, timedBoostManager, outputAugmentationManager, advancedSettingsManager);
        WorksiteManager worksiteManager = new(inventory, balanceManager, itemRegistry, timedBoostManager, outputAugmentationManager, advancedSettingsManager);

        ItemManager itemManager = new();
        CatalogManager catalogManager = new();
        InstantUnlimitedManager instantUnlimitedManager = new(cropManager, treeManager, animalManager, inventory, itemManager);
        RentalManager rentalManager = new(treeManager, animalManager, workshopManager, worksiteManager, instantUnlimitedManager, rulesManager);

        var profile = inventoryFactory.GetInventoryProfile(farm);
        UpgradeManager upgradeManager = new(rulesManager, inventory, profile,
            cropManager, animalManager, treeManager, workshopManager);
        ProgressionManager progressionManager = new(inventory, cropManager, animalManager, treeManager, workshopManager, worksiteManager, catalogManager, rulesManager);

        StoreManager storeManager = new(
            progressionManager, treeManager, animalManager, workshopManager, worksiteManager,
            catalogManager, inventory, instantUnlimitedManager,
            timedBoostManager, rentalManager, rulesManager
        );

        QuestManager questManager = new(inventory, itemManager, progressionManager, rulesManager);
        ScenarioManager scenarioManager = new(inventory, cropManager, treeManager, animalManager, workshopManager, worksiteManager);
        RandomChestManager randomChestManager = new(inventory, timedBoostManager);

        AchievementManager achievementManager = new(
            inventory, workshopManager, worksiteManager, progressionManager,
            timedBoostManager, animalManager, questManager, rulesManager, null, null
        );
        IGameTimer timer = new BasicGameState(
            rulesManager,
            inventory, inventoryFactory,
            cropFactory, treeFactory, animalFactory, workshopFactory,
            worksiteFactory, workerFactory, questFactory,
            upgradeFactory, progressionFactory, catalogFactory,
            storeFactory, itemFactory, instantUnlimitedFactory,
            timedBoostFactory, outputAugmentationFactory, rentalFactory, scenarioFactory, randomChestFactory, achievementFactory, advancedSettingsFactory,
            cropManager, treeManager, animalManager,
            workshopManager, worksiteManager, questManager,
            upgradeManager, progressionManager, catalogManager,
            storeManager, itemManager, instantUnlimitedManager,
            timedBoostManager, outputAugmentationManager, rentalManager, scenarioManager, randomChestManager, achievementManager, advancedSettingsManager,
            balanceManager, farm, category
        );
        return timer;

    }
    private IGameTimer GetAutomatedTimer(FarmKey farm, EnumRuleCategory category)
    {
        ItemRegistry itemRegistry = new();
        InventoryManager inventory = new(farm, inventoryRepository, itemRegistry);
        RulesManager rulesManager = new();
        AdvancedSettingsManager advancedSettingsManager = new();
        TimedBoostManager timedBoostManager = new(advancedSettingsManager);
        OutputAugmentationManager outputAugmentationManager = new();
        BalanceManager balanceManager = new(_baseBalanceProvider, rulesManager);


        // IMPORTANT: start as throwing (or no-op) so it's never null
        Func<string, Task> promote = _ => throw new InvalidOperationException("Promotion not wired yet.");

        AdvancedUpgradeAutomationManager advancedUpgradeAutomationManager =
            new(inventory, target => promote(target));
        CropAutomationManager cropManager = new(inventory, balanceManager, rulesManager, itemRegistry, 
            timedBoostManager, outputAugmentationManager, advancedUpgradeAutomationManager); //for now did not need any other (will in the future)
        TreeAutomationManager treeManager = new(inventory, balanceManager, rulesManager, itemRegistry, 
            timedBoostManager, outputAugmentationManager, advancedUpgradeAutomationManager);
        AnimalAutomationManager animalManager = new(inventory, balanceManager, rulesManager, itemRegistry, 
            timedBoostManager, outputAugmentationManager, advancedUpgradeAutomationManager);
        WorkshopAutomationManager workshopManager = new(inventory, balanceManager, rulesManager, itemRegistry,
            timedBoostManager, outputAugmentationManager, advancedUpgradeAutomationManager);
        WorksiteAutomationManager worksiteManager = new(inventory,  balanceManager, rulesManager, itemRegistry, 
            timedBoostManager, outputAugmentationManager, advancedUpgradeAutomationManager);

        ItemManager itemManager = new();
        CatalogManager catalogManager = new();
        InstantUnlimitedManager instantUnlimitedManager = new(cropManager, treeManager, animalManager, inventory, itemManager);
        promote = instantUnlimitedManager.UpgradeToInstantUnlimitedAsync;
        RentalManager rentalManager = new(null, null, null, worksiteManager, instantUnlimitedManager, rulesManager);

        var profile = inventoryFactory.GetInventoryProfile(farm);
        UpgradeManager upgradeManager = new(rulesManager, inventory, profile,
            null, null, null, null);
        
        ProgressionManager progressionManager = new(inventory, cropManager, animalManager, treeManager, workshopManager, worksiteManager, catalogManager, rulesManager);

        StoreManager storeManager = new(
            progressionManager, treeManager, animalManager, workshopManager, worksiteManager,
            catalogManager, inventory, instantUnlimitedManager,
            timedBoostManager, rentalManager, rulesManager
        );
        QuestManager questManager = new(inventory, itemManager, progressionManager, rulesManager);
        ScenarioManager scenarioManager = new(inventory);
        RandomChestManager randomChestManager = new(inventory, timedBoostManager);
        AchievementManager achievementManager = new(
            inventory, null, null, progressionManager,
            timedBoostManager, null, questManager, rulesManager, instantUnlimitedManager, advancedUpgradeAutomationManager
        );
        GiftManager giftManager = new(inventory, rulesManager, giftLedgerService);
        IGameTimer timer = new AutomatedGameState(
            rulesManager,
            inventory, inventoryFactory,
            cropAutomationFactory, treeAutomationFactory, animalAutomationFactory, workshopAutomationFactory,
            worksiteAutomationFactory, workerFactory, questFactory,
            upgradeFactory, progressionFactory, catalogFactory,
            storeFactory, itemFactory, instantUnlimitedFactory,
            timedBoostFactory, outputAugmentationFactory, rentalFactory, scenarioFactory, 
            randomChestFactory, achievementFactory, advancedSettingsFactory, 
            advancedUpgradesAutomationFactory, giftFactory,
            cropManager, treeManager, animalManager,
            workshopManager, worksiteManager, questManager,
            upgradeManager, progressionManager, catalogManager,
            storeManager, itemManager, instantUnlimitedManager,
            timedBoostManager, outputAugmentationManager, rentalManager, scenarioManager, 
            randomChestManager, achievementManager, advancedSettingsManager,
            balanceManager, advancedUpgradeAutomationManager,
            giftManager, farm, category
        );
        return timer;
    }
    async Task IGameTickRunner.InitializeFarmsAsync(string playerName, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Must have rules by the time this is called
        EnumRuleCategory category = await rulesProvider.GetRulesAsync(playerName);
        if (category == EnumRuleCategory.NotChosen)
        {
            throw new CustomBasicException($"Still did not specify the farm rules for {playerName}.");
        }
        BasicList<FarmKey> farms = await farmRegistry.GetFarmsAsync();
        await farms.ForConditionalItemsAsync(x => x.PlayerName == playerName, async farm =>
        {
            if (ShouldInitializeFarm(farm, category) == false)
            {
                return;
            }
            if (farm.IsMain)
            {
                await _startingOptionsCoordinatorService.ApplyAsync(farm, category == EnumRuleCategory.Automated);
            }
            await InitializeFarmInternalAsync(farm, category, token);
        });   
    }

    private static bool ShouldInitializeFarm(FarmKey farm, EnumRuleCategory category)
    {
        // Automation category: no cooperative farm (partner is the automation)
        if (category == EnumRuleCategory.Automated && farm.Slot == EnumFarmSlot.Cooperative)
        {
            return false;
        }

        return true;
    }

    async Task IGameTickRunner.TickOnceAsync(CancellationToken token)
    {
        await gameRegistry.TickAsync();
    }
}
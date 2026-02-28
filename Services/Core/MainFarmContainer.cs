namespace FarmSim.Domain.Services.Core;
public class MainFarmContainer
{
    required public FarmKey FarmKey { get; set; }
    required public InventoryManager InventoryManager { get; set; }
    public CropManager CropManager { get; set; } = null!;
    public TreeManager TreeManager { get; set; } = null!;
    public AnimalManager AnimalManager { get; set; } = null!;
    public WorkshopManager WorkshopManager { get; set; } = null!;
    public WorksiteManager WorksiteManager { get; set; } = null!; //for future.
    required public QuestManager QuestManager { get; set; }
    required public ScenarioManager ScenarioManager { get; set; }
    required public UpgradeManager UpgradeManager { get; set; }
    required public ProgressionManager ProgressionManager { get; set; }
    required public CatalogManager CatalogManager { get; set; }
    required public StoreManager StoreManager { get; set; }
    required public InstantUnlimitedManager InstantUnlimitedManager { get; set; }
    required public TimedBoostManager TimedBoostManager { get; set; }
    required public ItemManager ItemManager { get; set; }
    required public OutputAugmentationManager OutputAugmentationManager { get; set; }
    required public RentalManager RentalManager { get; set; }
    required public RandomChestManager RandomChestManager { get; set; }
    required public AchievementManager AchievementManager { get; set; }
    required public AdvancedSettingsManager AdvancedSettingsManager { get; set; }
    required public RulesManager RulesManager { get; set; }
    required public BalanceManager BalanceManager { get; set; }

    public TreeAutomationManager TreeAutomationManager { get; set; } = null!;
    public CropAutomationManager CropAutomationManager { get; set; } = null!;
    public AnimalAutomationManager AnimalAutomationManager { get; set; } = null!;
    public WorkshopAutomationManager WorkshopAutomationManager { get; set; } = null!;
    public WorksiteAutomationManager WorksiteAutomationManager { get; set; } = null!;
    public AdvancedUpgradeAutomationManager AdvancedUpgradeAutomationManager { get; set; } = null!;
    public GiftManager GiftManager { get; set; } = null!;
    //attempt to not require itemmanager here (since only the quest manager should require it.   if i am wrong, rethink).
}
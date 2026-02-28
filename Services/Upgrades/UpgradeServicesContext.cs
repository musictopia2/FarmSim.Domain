namespace FarmSim.Domain.Services.Upgrades;
public class UpgradeServicesContext
{
    required public IInventoryStorageUpgradePlanProvider InventoryStorageUpgradePlanProvider { get; init; }
    required public IWorkshopCapacityUpgradePlanProvider WorkshopCapacityUpgradePlanProvider { get; init; }
    required public IAdvancedUpgradePlanProvider AdvancedUpgradePlanProvider { get; init; }
    required public IWorkshopAdvancedUpgradePlanProvider WorkshopAdvancedUpgradePlanProvider { get; init; }

    //this is everything that needs to be resolved so the upgrade manager can do its job.

}
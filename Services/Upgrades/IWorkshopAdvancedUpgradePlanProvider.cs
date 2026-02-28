namespace FarmSim.Domain.Services.Upgrades;
public interface IWorkshopAdvancedUpgradePlanProvider
{
    Task<BasicList<WorkshopAdvancedUpgradeRuleModel>> GetPlansAsync(FarmKey farm);
}
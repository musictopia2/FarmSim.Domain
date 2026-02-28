namespace FarmSim.Domain.Services.Automation.Upgrades;
public interface IAdvancedUpgradeAutomationPlanProvider
{
    Task<BasicList<AdvancedUpgradeAutomationPlanModel>> GetPlansAsync(FarmKey farm);
}
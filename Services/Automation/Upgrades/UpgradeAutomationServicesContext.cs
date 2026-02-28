namespace FarmSim.Domain.Services.Automation.Upgrades;
public class UpgradeAutomationServicesContext
{
    public required IAdvancedUpgradeAutomationPlanProvider AdvancedUpgradeAutomationPlanProvider { get; set; }
    public required IAdvancedUpgradeAutomationProfile AdvancedUpgradeAutomationProfile { get; set; }
}
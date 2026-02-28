namespace FarmSim.Domain.Services.Automation.Upgrades;
public interface IAdvancedUpgradesAutomationFactory
{
    UpgradeAutomationServicesContext GetUpgradeServices(FarmKey farm);
}
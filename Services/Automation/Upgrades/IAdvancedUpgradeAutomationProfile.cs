namespace FarmSim.Domain.Services.Automation.Upgrades;
public interface IAdvancedUpgradeAutomationProfile
{
    Task<BasicList<AdvancedUpgradeAutomationOwnedModel>> LoadAsync();
    Task SaveAsync(BasicList<AdvancedUpgradeAutomationOwnedModel> owns);
}
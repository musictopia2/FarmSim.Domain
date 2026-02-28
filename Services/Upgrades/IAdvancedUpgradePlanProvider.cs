namespace FarmSim.Domain.Services.Upgrades;
public interface IAdvancedUpgradePlanProvider
{
    Task<BasicList<AdvancedUpgradePlanModel>> GetPlansAsync(FarmKey farm);
}
namespace FarmSim.Domain.Services.Upgrades;
public interface IWorkshopCapacityUpgradePlanProvider
{
    Task<BasicList<WorkshopCapacityUpgradePlanModel>> GetPlansAsync(FarmKey farm);
}
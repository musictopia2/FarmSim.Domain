namespace FarmSim.Domain.Services.Upgrades;
public class WorkshopCapacityUpgradePlanModel
{
    public string WorkshopName { get; init; } = "";
    public BasicList<UpgradeTier> Upgrades { get; init; } = [];
}
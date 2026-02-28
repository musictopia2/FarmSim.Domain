namespace FarmSim.Domain.Services.Upgrades;
public class AdvancedUpgradeTier
{
    //this time, it shows how much faster than original.
    public required double SpeedBonus { get; init; }
    public required Dictionary<string, int> Cost { get; init; } = [];
}
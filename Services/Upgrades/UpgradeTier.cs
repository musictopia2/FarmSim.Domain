namespace FarmSim.Domain.Services.Upgrades;
public class UpgradeTier
{
    public int Size { get; init; }    // capacity after reaching this level
    public Dictionary<string, int> Cost { get; init; } = []; // cost to reach THIS level
}
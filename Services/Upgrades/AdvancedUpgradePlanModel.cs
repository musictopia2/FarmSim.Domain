namespace FarmSim.Domain.Services.Upgrades;
public class AdvancedUpgradePlanModel
{
    public required EnumAdvancedUpgradeTrack Category { get; init; }
    public required double? ExtraOutputChance { get; init; } //only workshops will use it.
    public required BasicList<AdvancedUpgradeTier> Tiers { get; init; }
}
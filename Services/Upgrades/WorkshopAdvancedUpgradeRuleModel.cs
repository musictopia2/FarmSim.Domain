namespace FarmSim.Domain.Services.Upgrades;
public class WorkshopAdvancedUpgradeRuleModel
{
    public required string BuildingName { get; init; }

    // Absolute player levels required for each tier (same count as your workshop tiers: 5)
    public required BasicList<int> TierLevelRequired { get; init; }
}
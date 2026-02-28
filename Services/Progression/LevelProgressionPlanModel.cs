namespace FarmSim.Domain.Services.Progression;
public class LevelProgressionPlanModel
{
    /// <summary>
    /// If true: levels continue forever. When tiers run out, the last tier repeats.
    /// </summary>
    public bool IsEndless { get; init; }

    /// <summary>
    /// Ordered list of level tiers (Level 1 uses Tiers[0], Level 2 uses Tiers[1], etc.).
    /// </summary>
    public BasicList<LevelProgressionTier> Tiers { get; init; } = [];
}
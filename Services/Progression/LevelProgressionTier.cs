namespace FarmSim.Domain.Services.Progression;
public class LevelProgressionTier
{
    /// <summary>
    /// How many progress points (quests) are required to complete this level.
    /// </summary>
    public int RequiredPoints { get; init; }

    /// <summary>
    /// Optional milestone rewards during the level (e.g. 20%, 50%, 80%).
    /// If empty, no progress milestone rewards for this level.
    /// </summary>
    public BasicList<ProgressMilestoneReward> ProgressMilestoneRewards { get; init; } = [];

    /// <summary>
    /// Rewards granted when the level is completed (when PointsThisLevel reaches RequiredPoints).
    /// </summary>
    public Dictionary<string, int> RewardsOnLevelComplete { get; init; } = [];
}

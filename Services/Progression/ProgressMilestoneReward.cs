namespace FarmSim.Domain.Services.Progression;
public class ProgressMilestoneReward
{
    /// <summary>
    /// Milestone percentage (1..100). Example: 20 means 20% progress.
    /// </summary>
    public int Percent { get; init; }

    /// <summary>
    /// Rewards granted when this milestone is reached.
    /// </summary>
    public Dictionary<string, int> Rewards { get; init; } = [];
}

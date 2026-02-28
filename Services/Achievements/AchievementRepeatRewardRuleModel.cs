namespace FarmSim.Domain.Services.Achievements;
public class AchievementRepeatRewardRuleModel
{
    public BasicList<int> FirstCoinRewards { get; set; } = [];  // e.g. [5, 8]
    // Reward for EVERY tier after the explicit ones (fixed).
    // If null, you can default to the last FirstCoinRewards value.
    public int? CoinRewardAfterFirst { get; set; }
}
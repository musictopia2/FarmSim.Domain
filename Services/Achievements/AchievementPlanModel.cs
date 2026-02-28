namespace FarmSim.Domain.Services.Achievements;
public class AchievementPlanModel
{
    //if leveling up, then the counter key is level.
    public string CounterKey { get; set; } = ""; //what happened (event family)
    public string SourceKey { get; set; } = ""; //where/how it happens (like pond, mines)
    public string? OutputAugmentationKey { get; set; }
    public string ItemKey { get; set; } = ""; //what it was all about

    public int? Target { get; set; } //non repeatable amount you need to get this achievement
    public int? CoinReward { get; set; } //non repeatable amount of coin you receive
    public AchievementRepeatRuleModel? RepeatAchievementRules { get; set; }
    public AchievementRepeatRewardRuleModel? RepeatRewardRules { get; set; }
}
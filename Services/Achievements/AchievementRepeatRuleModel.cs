namespace FarmSim.Domain.Services.Achievements;
public class AchievementRepeatRuleModel
{
    public BasicList<int> FirstTargets { get; set; } = [];
    public int IncrementAfterFirst { get; set; }
}
namespace FarmSim.Domain.Services.Achievements;
public class AchievementServicesContext
{
    public required IAchievementPlanProvider AchievementPlanProvider { get; set; }
    public required IAchievementProfile AchievementProfile { get; set; }
}
namespace FarmSim.Domain.Services.Achievements;
public interface IAchievementPlanProvider
{
    Task<BasicList<AchievementPlanModel>> GetPlanAsync(FarmKey farm, bool automated);
}
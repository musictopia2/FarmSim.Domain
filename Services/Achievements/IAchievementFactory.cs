namespace FarmSim.Domain.Services.Achievements;
public interface IAchievementFactory
{
    AchievementServicesContext GetAchievementServices(FarmKey farm);
}
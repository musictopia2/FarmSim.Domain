namespace FarmSim.Domain.Services.Achievements;
public interface IAchievementProfile
{
    Task<AchievementProfileModel> LoadAsync();
    Task SaveAsync(AchievementProfileModel profile);
}
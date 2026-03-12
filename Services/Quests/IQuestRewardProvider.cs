namespace FarmSim.Domain.Services.Quests;
public interface IQuestRewardProvider
{
    Task<BasicList<QuestRewardRow>> GetRewardsAsync(FarmKey farm, bool automated);
}
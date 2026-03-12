namespace FarmSim.Domain.Services.Quests;
public interface IQuestRewardProvider
{
    Task<BasicList<QuestRewardRowModel>> GetRewardsAsync(FarmKey farm, bool automated);
}
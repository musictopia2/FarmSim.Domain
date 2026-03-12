namespace FarmSim.Domain.Services.Quests;
public interface IQuestGenerationService
{
    QuestInstanceModel CreateQuest(int currentLevel,
        BasicList<QuestRewardRow> rewards,
        BasicList<CompiledQuestItemRow> allItems,
        BasicList<CategoryWeightRow> categories);
}
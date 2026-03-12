namespace FarmSim.Domain.Services.Quests;
public interface IQuestGenerationService
{
    QuestInstanceModel CreateQuest(int currentLevel,
        BasicList<QuestRewardRowModel> rewards,
        BasicList<CompiledQuestItemRowModel> allItems,
        BasicList<CategoryWeightRowModel> categories);
}
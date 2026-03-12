namespace FarmSim.Domain.Services.Quests;
public class QuestServicesContext
{
    public required IQuestProfile QuestProfile { get; init; }
    public required IQuestGenerationService QuestGenerationService { get; init; }
    public required IOrderBoardSizeProvider OrderBoardSizeProvider { get; init; }
    public required ICategoryWeightProvider CategoryWeightProvider { get; init; }
    public required ICompiledQuestBalanceProvider CompiledQuestBalanceProvider { get; init; }
    public required IQuestRewardProvider QuestRewardProvider { get; init; }
}
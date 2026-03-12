namespace FarmSim.Domain.Services.Quests;
public interface IQuestFactory
{
    QuestServicesContext GetQuestServices(FarmKey farm, bool automated);
}
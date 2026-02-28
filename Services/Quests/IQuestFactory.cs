namespace FarmSim.Domain.Services.Quests;
public interface IQuestFactory
{
    QuestServicesContext GetQuestServices(FarmKey farm, ICropManager cropManager,
        ITreeManager treeManager, IAnimalManager animalManager,
        IWorkshopManager workshopManager,
        ItemManager itemManager,
        RulesManager rulesManager
        );
}
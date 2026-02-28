namespace FarmSim.Domain.Services.Scenarios;
public interface IScenarioFactory
{
    //the quest one needed the crop and tree manager.  hopefully this won't need it  (not sure yet).
    ScenarioServicesContext GetScenarioServices(FarmKey farm, 
        InstantUnlimitedManager instantUnlimitedManager, //keep this so a person can use for testing if they want.
        ICropManager cropManager,
        ITreeManager treeManager,
        IAnimalManager animalManager,
        IWorkshopManager workshopManager,
        IWorksiteManager worksiteManager,
        ItemManager itemManager
        );
}
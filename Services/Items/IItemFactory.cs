namespace FarmSim.Domain.Services.Items;
public interface IItemFactory
{
    ItemServicesContext GetItemServices(FarmKey farm,
        ICropRecipes cropdb,
        ITreeRecipes treedb,
        IAnimalRecipes animaldb,
        IWorkshopRecipes workshopdb,
        IWorksiteRecipes worksitedb,
        ICropProgressionPlanProvider cropPlanDb,
        ICatalogDataSource catalogDb,
        IAnimalProgressionPlanProvider animalPlanDb,
        IWorkshopProgressionPlanProvider workshopPlanDb
        );
}
namespace FarmSim.Domain.Services.Items;
public class ItemFactory : IItemFactory
{
    ItemServicesContext IItemFactory.GetItemServices(FarmKey farm, ICropRecipes cropdb, 
        ITreeRecipes treedb, IAnimalRecipes animaldb, 
        IWorkshopRecipes workshopdb, IWorksiteRecipes worksitedb, 
        ICropProgressionPlanProvider cropPlanDb, ICatalogDataSource catalogDb,
        IAnimalProgressionPlanProvider animalPlanDb, IWorkshopProgressionPlanProvider workshopPlanDb)
    {
        return new()
        {
            ItemPlanProvider = new ComputedItemPlanProvider(cropdb,
            treedb, animaldb,
            workshopdb, worksitedb,
            cropPlanDb, catalogDb,
            animalPlanDb, workshopPlanDb
            )
        };
    }
}
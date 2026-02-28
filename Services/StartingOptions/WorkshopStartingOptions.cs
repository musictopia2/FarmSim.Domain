namespace FarmSim.Domain.Services.StartingOptions;
public class WorkshopStartingOptions(IWorkshopFactory oldFactory, IWorkshopAutomationFactory newFactory,
    IProgressionFactory progressionFactory, ICatalogFactory catalogFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var newContext = newFactory.GetWorkshopServices(farm);
        BasicList<WorkshopRecipe> recipes = await newContext.WorkshopRecipes.GetWorkshopRecipesAsync();
        var source = catalogFactory.GetCatalogServices(farm).CatalogDataSource;
        var catalogList = await source.GetCatalogAsync(farm);
        catalogList.KeepConditionalItems(x => x.Category == EnumCatalogCategory.Workshop && x.Costs.Count == 0);
        var oldRepository = oldFactory.GetWorkshopServices(farm).WorkshopRespository;
        var temp = await progressionFactory.GetProgressionServices(farm).ProgressionProfile.LoadAsync();
        var level = temp.Level;
        BasicList<WorkshopAutoResumeModel> oldInstances = await oldRepository.LoadAsync();
        oldInstances.Clear(); //not here anymore.
        await oldRepository.SaveAsync(oldInstances);
        BasicList<WorkshopAutomationBuildingLaneStateModel> automationList = [];
        var plans = await progressionFactory.GetProgressionServices(farm).WorkshopProgressionPlanProvider.GetPlanAsync(farm);
        var buildings = recipes.DistinctBy(x => x.BuildingName).ToBasicList();
        foreach (var catalogOffer in catalogList)
        {
            bool unlocked = level >= catalogOffer.LevelRequired;
            WorkshopAutomationBuildingLaneStateModel workshop = new()
            {
                BuildingName = catalogOffer.TargetName,
                Unlocked = unlocked,
                VirtualCount = 1
            };
            recipes.ForConditionalItems(x => x.BuildingName == catalogOffer.TargetName, recipe =>
            {
                var plan = plans.Single(x => x.ItemName == recipe.Item);
                unlocked = level >= plan.LevelRequired;
                workshop.Items.Add(new()
                {
                    BuildingName = catalogOffer.TargetName,
                    Item = recipe.Item,
                    Unlocked = unlocked
                });
            });
            automationList.Add(workshop);
        }
        var db = newContext.WorkshopAutomationProfile;
        await db.SaveAsync(automationList);
    }
}
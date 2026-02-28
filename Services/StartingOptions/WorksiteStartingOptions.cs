namespace FarmSim.Domain.Services.StartingOptions;
public class WorksiteStartingOptions(IWorksiteFactory oldFactory, IWorksiteAutomationFactory newFactory,
    IProgressionFactory progressionFactory, ICatalogFactory catalogFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var newContext = newFactory.GetWorksiteServices(farm);
        BasicList<WorksiteRecipe> recipes = await newContext.WorksiteRecipes.GetWorksitesAsync();
        var source = catalogFactory.GetCatalogServices(farm).CatalogDataSource;
        var catalogList = await source.GetCatalogAsync(farm);
        catalogList.KeepConditionalItems(x => x.Category == EnumCatalogCategory.Worksite && x.Costs.Count == 0);
        var oldRepository = oldFactory.GetWorksiteServices(farm).WorksiteRepository;
        var temp = await progressionFactory.GetProgressionServices(farm).ProgressionProfile.LoadAsync();
        var level = temp.Level;
        BasicList<WorksiteAutoResumeModel> oldInstances = await oldRepository.LoadAsync();
        oldInstances.Clear(); //not here anymore.
        await oldRepository.SaveAsync(oldInstances);
        BasicList<WorksiteAutomationStateModel> automationList = [];
        foreach (var item in catalogList)
        {
            bool unlocked = level >= item.LevelRequired;
            automationList.Add(new()
            {
                Location = item.TargetName,
                Unlocked = unlocked
            });
        }
        var db = newContext.WorksiteAutomationProfile;
        await db.SaveAsync(automationList);
    }
}
namespace FarmSim.Domain.Services.StartingOptions;
public class TreeStartingOptionsService(ITreeFactory oldFactory, ITreeAutomationFactory newFactory,
    ICatalogFactory catalogFactory,
    IProgressionFactory progressionFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var newContext = newFactory.GetTreeServices(farm);
        BasicList<TreeRecipe> recipes = await newContext.TreeRecipes.GetTreesAsync();
        var source = catalogFactory.GetCatalogServices(farm).CatalogDataSource;
        var catalogList = await source.GetCatalogAsync(farm);
        catalogList.KeepConditionalItems(x => x.Category == EnumCatalogCategory.Tree && x.Costs.Count == 0);
        var oldRepository = oldFactory.GetTreeServices(farm).TreeRepository;
        var temp = await progressionFactory.GetProgressionServices(farm).ProgressionProfile.LoadAsync();
        var level = temp.Level;
        BasicList<TreeAutoResumeModel> oldInstances = await oldRepository.LoadAsync();
        oldInstances.Clear(); //not here anymore.
        await oldRepository.SaveAsync(oldInstances);
        BasicList<TreeAutomationStateModel> automationList = [];
        foreach (var recipe in recipes)
        {
            var catalogItem = catalogList.Single(x => x.TargetName == recipe.TreeName);
            bool unlocked = level >= catalogItem.LevelRequired;
            TreeAutomationStateModel state = new()
            {
                TreeName = recipe.TreeName,
                Unlocked = unlocked
            };
            state.VirtualCount = 2;
            automationList.Add(state);
        }
        var db = newContext.Profile;
        await db.SaveAsync(automationList);
    }
}
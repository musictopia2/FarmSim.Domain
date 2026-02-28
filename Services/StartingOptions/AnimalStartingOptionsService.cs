namespace FarmSim.Domain.Services.StartingOptions;
public class AnimalStartingOptionsService(IAnimalFactory oldFactory, IAnimalAutomationFactory newFactory,
    IProgressionFactory progressionFactory, ICatalogFactory catalogFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var newContext = newFactory.GetAnimalServices(farm);
        BasicList<AnimalRecipe> recipes = await newContext.AnimalRecipes.GetAnimalsAsync();
        var source = catalogFactory.GetCatalogServices(farm).CatalogDataSource;
        var catalogList = await source.GetCatalogAsync(farm);
        catalogList.KeepConditionalItems(x => x.Category == EnumCatalogCategory.Animal && x.Costs.Count == 0);
        var oldRepository = oldFactory.GetAnimalServices(farm).AnimalRepository;
        var temp = await progressionFactory.GetProgressionServices(farm).ProgressionProfile.LoadAsync();
        var level = temp.Level;
        BasicList<AnimalAutoResumeModel> oldInstances = await oldRepository.LoadAsync();
        oldInstances.Clear(); //not here anymore.
        await oldRepository.SaveAsync(oldInstances);
        BasicList<AnimalAutomationStateModel> automationList = [];
        foreach (var recipe in recipes)
        {
            var catalogItem = catalogList.Single(x => x.TargetName == recipe.Animal);
            bool unlocked = level >= catalogItem.LevelRequired;
            AnimalAutomationStateModel state = new()
            {
                AnimalName = recipe.Animal,
                Unlocked = unlocked
            };
            state.VirtualCount = 1; //you only receive 1.
            automationList.Add(state);
        }
        var db = newContext.AnimalAutomationProfile;
        await db.SaveAsync(automationList);
    }
}
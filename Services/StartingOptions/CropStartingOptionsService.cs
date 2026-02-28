namespace FarmSim.Domain.Services.StartingOptions;
public class CropStartingOptionsService(ICropFactory oldFactory, ICropAutomationFactory newFactory,
    IProgressionFactory progressionFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var newContext = newFactory.GetCropServices(farm);
        BasicList<CropRecipe> recipes = await newContext.CropRecipes.GetCropsAsync();
        var oldRepository = oldFactory.GetCropServices(farm).CropRepository;
        var temp = await progressionFactory.GetProgressionServices(farm).ProgressionProfile.LoadAsync();
        var level = temp.Level;
        CropSystemState oldInstances = await oldRepository.LoadAsync();
        oldInstances.Crops.Clear();
        oldInstances.Slots.Clear();
        await oldRepository.SaveAsync(oldInstances);
        BasicList<CropAutomationStateModel> automationList = [];
        var cropProgresion = progressionFactory.GetProgressionServices(farm).CropProgressionPlanProvider;
        var cropRequirements = await cropProgresion.GetPlanAsync(farm);
        foreach (var recipe in recipes)
        {
            var item = cropRequirements.UnlockRules.Single(x => x.ItemName == recipe.Item);
            bool unlocked = level >= item.LevelRequired;
            CropAutomationStateModel state = new()
            {
                Name = recipe.Item,
                Unlocked = unlocked
            };
            state.VirtualCount = 2; //decided to go ahead and let there be the second wheat field after all.
            automationList.Add(state);
        }
        var db = newContext.CropAutomationProfile;
        await db.SaveAsync(automationList);
    }
}
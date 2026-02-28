namespace FarmSim.Domain.Services.StartingOptions;
public class AdvancedSettingsStartingOptionsService(IAdvancedSettingsFactory advancedSettingsFactory)
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var context = advancedSettingsFactory.GetAdvancedSettingsServices(farm).AdvancedSettingsProfile;
        var setting = await context.LoadAsync();

        setting.AnimalCollectionPolicy = EnumAnimalCollectionMode.AllAtOnce;
        setting.AutomateWorksiteCollection = true;
        //this is temporary until i do something better.
        setting.AnimalDefaultMode = EnumAnimalDefaultOption.Slowest;
        setting.UseConfirmations = false;
        setting.AutomateCropCollection = true;
        setting.CollectAllAvailableFromTrees = true;
        setting.UseBoostImmediatelyUponPurchase = true;
        setting.Crafting.AutoUseFinishResources = true;
        setting.Crafting.EnableBulkCraftButton = true;
        setting.Crafting.WorkshopCollectionPolicy = EnumWorkshopCollectionCategories.FullyAutomated;
        await context.SaveAsync(setting);
    }
}
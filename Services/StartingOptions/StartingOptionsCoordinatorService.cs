namespace FarmSim.Domain.Services.StartingOptions;
public class StartingOptionsCoordinatorService(InventoryStorageStartingOptionsService inventoryStorage,
    AdvancedSettingsStartingOptionsService advancedSettings,
    TreeStartingOptionsService treeStart, CropStartingOptionsService cropStart,
    AnimalStartingOptionsService animalStarting,
    WorkshopStartingOptions workshopStarting,
    WorksiteStartingOptions worksiteStarting,
    AchievementsStartingOptionsService achievementsStartingOptionsService
    )
{
    public async Task ApplyAsync(FarmKey farm, bool automated)
    {
        if (automated == false)
        {
            return;
        }
        if (farm.IsCoin == false)
        {
            await inventoryStorage.ApplyAutomatedOptionsAsync(farm); //coin already has something else.
            await treeStart.ApplyAutomatedOptionsAsync(farm); //coin can never do automation.
            await cropStart.ApplyAutomatedOptionsAsync(farm);
            await animalStarting.ApplyAutomatedOptionsAsync(farm);
            await workshopStarting.ApplyAutomatedOptionsAsync(farm);
            await worksiteStarting.ApplyAutomatedOptionsAsync(farm);
            await achievementsStartingOptionsService.ApplyAutomatedOptionsAsync(farm);
        }
        await advancedSettings.ApplyAutomatedOptionsAsync(farm);
    }
}
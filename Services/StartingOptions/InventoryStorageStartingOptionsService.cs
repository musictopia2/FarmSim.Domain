namespace FarmSim.Domain.Services.StartingOptions;
public class InventoryStorageStartingOptionsService(
     IInventoryFactory inventoryFactory,
    IUpgradeFactory upgradeFactory
    )
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var storageProfileSvc = inventoryFactory.GetInventoryProfile(farm);
        var storage = await storageProfileSvc.LoadAsync();
        var upgradeCtx = upgradeFactory.GetUpgradeServices(farm);
        var plan = await upgradeCtx.InventoryStorageUpgradePlanProvider.GetPlanAsync(farm, true);
        storage.BarnSize = plan.BarnUpgrades.First().Size;
        storage.SiloSize = plan.SiloUpgrades.First().Size;


        var service = inventoryFactory.GetInventoryServices(farm);
        var list = await service.LoadAsync(farm);
        list.Clear();
        await service.SaveAsync(farm, list);
        await storageProfileSvc.SaveAsync(storage);
    }
}
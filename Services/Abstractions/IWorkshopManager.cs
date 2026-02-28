namespace FarmSim.Domain.Services.Abstractions;
public interface IWorkshopManager
{
    WorkshopRecipeQuestModel GetWorkshopForQuests(string itemCrafting);
    bool IsInBuilding(string buildingName, string itemToCheck);
    void ApplyWorkshopProgressionOnLevelUnlocks(BasicList<ItemUnlockRule> rules, BasicList<CatalogOfferModel> offers, int level);
    int GetUnlockedCount(string buildingName);
    void PurchaseWorkshop(StoreItemRowModel store);
}
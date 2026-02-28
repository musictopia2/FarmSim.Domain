namespace FarmSim.Domain.Services.Abstractions;
public interface ITreeManager
{
    void PurchaseTree(StoreItemRowModel store);
    void ApplyTreeUnlocksOnLevels(BasicList<CatalogOfferModel> offers, int level);
    TimeSpan GetTimeForGivenTree(string treeName);
    int GetUnlockedCount(string treeName);
    void SetTreeSuppressionByProducedItem(string itemName, bool supressed);
    void GrantUnlimitedTreeItems(GrantableItem item);
    //void ResetAllTreesToIdle();
}
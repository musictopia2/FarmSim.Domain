namespace FarmSim.Domain.Services.Abstractions;
public interface IAnimalManager
{
    void PurchaseAnimal(StoreItemRowModel store);
    TimeSpan GetTimePerOutputForGivenAnimal(string outputName);
    void ApplyAnimalProgressionUnlocksFromLevels(
        BasicList<ItemUnlockRule> rules,
        BasicList<CatalogOfferModel> offers,
        int level);
    AnimalProductionOption NextProductionOption(string animal);
    void SetAnimalSuppressionByProducedItem(string itemName, bool supressed);
    void GrantUnlimitedAnimalItems(GrantableItem item);
    int GetUnlockedCount(string animalName);
}
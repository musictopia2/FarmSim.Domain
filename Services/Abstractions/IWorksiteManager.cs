namespace FarmSim.Domain.Services.Abstractions;
public interface IWorksiteManager
{
    TimeSpan GetTimeForWorksiteItem(string itemName);
    Task UnlockWorkerAcquiredAsync(StoreItemRowModel store);
    Task DoubleCheckActiveWorkerRentalAsync(RentalInstanceModel rental);
    Task<bool> CanDeleteWorkerRentalAsync(RentalInstanceModel rental);
    void ApplyWorksiteProgressionUnlocksFromLevels(BasicList<CatalogOfferModel> offers, int level);
    Task ApplyWorkerProgressionUnlocksFromLevelsAsync(BasicList<CatalogOfferModel> offers, int level);
    int GetWorksiteUnlockedCount(string location);
    int GetUnlockedWorkersCount(string workerName);
}
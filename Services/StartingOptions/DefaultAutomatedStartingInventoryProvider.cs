namespace FarmSim.Domain.Services.StartingOptions;
public class DefaultAutomatedStartingInventoryProvider(IStartingInventoryOverrideProvider startingInventoryOverrideProvider) : IAutomatedStartingInventoryProvider
{
    Dictionary<string, int> IAutomatedStartingInventoryProvider.GetStartingInventory(FarmKey farm)
    {
        return startingInventoryOverrideProvider.GetOverrides(farm, automated: true);
    }
}
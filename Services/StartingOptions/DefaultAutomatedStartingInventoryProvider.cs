namespace FarmSim.Domain.Services.StartingOptions;
public class DefaultAutomatedStartingInventoryProvider : IAutomatedStartingInventoryProvider
{
    Dictionary<string, int> IAutomatedStartingInventoryProvider.GetStartingInventory(FarmKey farm)
    {
        return [];
    }
}
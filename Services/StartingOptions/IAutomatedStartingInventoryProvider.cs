namespace FarmSim.Domain.Services.StartingOptions;
public interface IAutomatedStartingInventoryProvider
{
    Dictionary<string, int> GetStartingInventory(FarmKey farm);
}
namespace FarmSim.Domain.Services.StartingOptions;

public class DefaultStartingInventoryOverrideProvider : IStartingInventoryOverrideProvider
{
    public Dictionary<string, int> GetOverrides(FarmKey farm, bool automated) => [];
}
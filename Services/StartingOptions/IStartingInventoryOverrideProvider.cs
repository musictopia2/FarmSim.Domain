namespace FarmSim.Domain.Services.StartingOptions;
public interface IStartingInventoryOverrideProvider
{
    Dictionary<string, int> GetOverrides(FarmKey farm, bool automated);
}
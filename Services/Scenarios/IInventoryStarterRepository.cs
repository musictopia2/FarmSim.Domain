namespace FarmSim.Domain.Services.Scenarios;
public interface IInventoryStarterRepository
{
    Task<Dictionary<string, int>> GetBaseLineAsync(FarmKey farm);
}
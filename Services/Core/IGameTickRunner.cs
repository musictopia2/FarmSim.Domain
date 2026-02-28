namespace FarmSim.Domain.Services.Core;
public interface IGameTickRunner
{
    Task InitializeAsync(CancellationToken token = default);
    //this is brand new now.
    Task InitializeFarmsAsync(string playerName, CancellationToken token = default);
    Task TickOnceAsync(CancellationToken token = default);
}
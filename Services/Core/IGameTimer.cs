namespace FarmSim.Domain.Services.Core;
public interface IGameTimer
{
    Task TickAsync();
    Task SetThemeContextAsync(FarmKey farm);
    MainFarmContainer FarmContainer { get; }
    FarmKey? FarmKey { get; }
}
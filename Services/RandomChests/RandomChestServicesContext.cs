namespace FarmSim.Domain.Services.RandomChests;
public class RandomChestServicesContext
{
    public required IRandomChestGenerator RandomChestGenerator { get; init; }
}
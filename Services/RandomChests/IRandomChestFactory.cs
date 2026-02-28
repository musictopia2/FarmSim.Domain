namespace FarmSim.Domain.Services.RandomChests;
public interface IRandomChestFactory
{
    //has to wait until i make more progress.
    RandomChestServicesContext GetRandomChestServices(FarmKey farm, ProgressionManager progressionManager);
}
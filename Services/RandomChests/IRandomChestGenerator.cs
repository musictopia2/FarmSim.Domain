namespace FarmSim.Domain.Services.RandomChests;
public interface IRandomChestGenerator
{
    Task<RandomChestResultModel> GenerateRewardAsync();
}

namespace FarmSim.Domain.Services.RandomChests;
public class RandomChestManager(InventoryManager inventoryManager, TimedBoostManager timedBoostManager)
{
    private IRandomChestGenerator _randomChestGenerator = null!;
    public void SetRandomChestStyleContext(RandomChestServicesContext context)
    {
        _randomChestGenerator = context.RandomChestGenerator;
    }
    public async Task<BasicList<RandomChestResultModel>> OpenChestsAsync()
    {
        int howMany = inventoryManager.Get(CurrencyKeys.Chest);
        BasicList<RandomChestResultModel> firsts = [];
        await howMany.TimesAsync(async x =>
        {
            firsts.Add(await _randomChestGenerator.GenerateRewardAsync());
        });
        await ProcessRewardsAsync(firsts);
        inventoryManager.Consume(CurrencyKeys.Chest, howMany);
        var output = GetCombinedResults(firsts);
        return output;
    }
    private async Task ProcessRewardsAsync(BasicList<RandomChestResultModel> results)
    {
        foreach (var item in results)
        {
            await ProcessRewardAsync(item);
        }
    }
    private async Task ProcessRewardAsync(RandomChestResultModel result)
    {
        if (result.Duration is null)
        {
            ItemAmount amount = new()
            {
                Amount = result.Quantity,
                Item = result.TargetName
            };
            inventoryManager.Add(amount);
            return;
        }
        await timedBoostManager.GrantCreditAsync(result);
    }
    private static BasicList<RandomChestResultModel> GetCombinedResults(BasicList<RandomChestResultModel> results)
    {
        // Key includes everything that makes a reward distinct *except* Quantity.
        // Quantity will be summed.
        var grouped = results
            .Where(x => x.TargetName is not null) // if you allow null, decide how you want to handle it
            .GroupBy(x => new RewardKey(
                TargetName: x.TargetName!,
                //Source: x.Source,
                OutputAugmentationKey: x.OutputAugmentationKey,
                DurationTicks: x.Duration?.Ticks,
                ReduceByTicks: x.ReducedBy?.Ticks
            ))
            .Select(g =>
            {
                var first = g.First();
                return new RandomChestResultModel
                {
                    TargetName = first.TargetName,
                    //Source = first.Source,
                    OutputAugmentationKey = first.OutputAugmentationKey,
                    Duration = first.Duration,
                    ReducedBy = first.ReducedBy,
                    Quantity = g.Sum(x => x.Quantity)
                };
            })
            .ToList();

        // Optional: sort for nicer UI (category then name, etc.)
        grouped.Sort((a, b) =>
        {
            return string.Compare(a.TargetName, b.TargetName, StringComparison.Ordinal);
        });

        return [.. grouped];
    }
    
    // Private key type for grouping.
    // Use ticks to avoid TimeSpan equality edge cases and to keep it value-like.
    private readonly record struct RewardKey(
        string TargetName,
        string? OutputAugmentationKey,
        long? DurationTicks,
        long? ReduceByTicks
    );
}
namespace FarmSim.Domain.Services.StartingOptions;
public class AchievementsStartingOptionsService(IAchievementFactory achievementFactory)
{
    public async Task ApplyAutomatedOptionsAsync(FarmKey farm)
    {
        var plandb = achievementFactory.GetAchievementServices(farm).AchievementPlanProvider;
        var profiledb = achievementFactory.GetAchievementServices(farm).AchievementProfile;
        var list = await plandb.GetPlanAsync(farm, true);
        var profile = await profiledb.LoadAsync();
        profile.InstantUnlimitedProgress.Clear();
        profile.ChestsOpened = 0;
        profile.WorkshopQueued.Clear();
        profile.CoinsEarned = 0;
        profile.AnimalCollectProgress.Clear();
        profile.AutomationUpgradeProgress.Clear();
        profile.CoinsSpent = 0;
        profile.Consumables.Clear();
        profile.OrderItemProgress.Clear();
        profile.OrdersCompleted = 0;
        profile.ScenariosCompleted = 0;
        profile.TimedBoostProgress.Clear();
        profile.TotalCoinsSpent = 0;
        profile.WorksiteFoundProgress.Clear();
        list.ForConditionalItems(x => x.CounterKey == AchievementCounterKeys.UseConsumable, item =>
        {
            profile.Consumables.Add(new()
            {
                Key = item.ItemKey,
            });
        });
        list.ForConditionalItems(x => x.CounterKey == AchievementCounterKeys.CompleteOrders && x.ItemKey != "", item =>
        {
            profile.OrderItemProgress.Add(new()
            {
                ItemName = item.ItemKey
            });
        });

        list.ForConditionalItems(x => x.CounterKey == AchievementCounterKeys.UseTimedBoost, item =>
        {
            profile.TimedBoostProgress.Add(new()
            {
                OutputAugmentationKey = item.OutputAugmentationKey,
                SourceKey = item.SourceKey
            });
        });

        list.ForConditionalItems(x => x.CounterKey == AchievementCounterKeys.SpecificInstantUnlimited, item =>
        {
            profile.InstantUnlimitedProgress.Add(new()
            {
                Item = item.ItemKey
            });
        });
        BasicList<string> upgrades =
            [
                AchievementCounterKeys.QueUpgrade,
                AchievementCounterKeys.ReducedTimeUpgrade,
                AchievementCounterKeys.VirtualCount
            ];
        foreach (var upgrade in upgrades)
        {
            list.ForConditionalItems(x => x.CounterKey == upgrade, item =>
            {
                profile.AutomationUpgradeProgress.Add(new()
                {
                    Counter = item.CounterKey,
                    Target = item.SourceKey
                });
            });
        }
        await profiledb.SaveAsync(profile);
    }
}
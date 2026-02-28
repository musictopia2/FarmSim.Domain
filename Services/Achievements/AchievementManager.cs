namespace FarmSim.Domain.Services.Achievements;
public class AchievementManager(InventoryManager inventoryManager,
    WorkshopManager? workshopManager,
    WorksiteManager? worksiteManager,
    ProgressionManager progressionManager,
    TimedBoostManager timedBoostManager,
    AnimalManager? animalManager,
    QuestManager questManager,
    RulesManager rulesManager,
    InstantUnlimitedManager? instantUnlimitedManager,
    AdvancedUpgradeAutomationManager? advancedUpgradeAutomationManager
    )
{
    private BasicList<AchievementPlanModel> _plans = [];
    private AchievementProfileModel _profileInfo = null!;
    private IAchievementProfile _profileStorage = null!;
    public event Action<string, int>? OnAchievementSuccessful;
    public async Task SetAchievementStyleContextAsync(AchievementServicesContext context, FarmKey farm)
    {
        _plans = await context.AchievementPlanProvider.GetPlanAsync(farm, rulesManager.AutomationEnabled);
        _profileStorage = context.AchievementProfile;
        _profileInfo = await _profileStorage.LoadAsync();
        inventoryManager.InventoryAdded += ProcessInventoryAdded;
        inventoryManager.InventoryConsumed += ProcessInventoryConsumed;
        workshopManager?.OnWorkshopItemQued += ProcessWorkshopQueAdded;
        worksiteManager?.OnRewardPickedUp += ProcessWorksiteRewards;
        progressionManager.OnIncreaseLevel += ProcessLevelIncrease;
        timedBoostManager.OnUsedTimedBoost += ProcessTimedBoost;
        animalManager?.OnAnimalCollected += ProcessAnimalCollected;
        questManager.OnOrderCompleted += ProcessOrderCompleted;
        instantUnlimitedManager?.OnUpgradedToInstantUnlimited += ProcessInstantUnlimited;
        advancedUpgradeAutomationManager?.OnUpgradePurchased += ProcessAdvancedUpgradeAutomation;
    }
    private async void ProcessAdvancedUpgradeAutomation(string target, EnumAdvancedUpgradeAutomationEffect effect, int level)
    {

        string toFind;
        if (effect == EnumAdvancedUpgradeAutomationEffect.QueueSize)
        {
            toFind = AchievementCounterKeys.QueUpgrade;
        }
        else if (effect == EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent)
        {
            toFind = AchievementCounterKeys.ReducedTimeUpgrade;
        }
        else if (effect == EnumAdvancedUpgradeAutomationEffect.VirtualCount)
        {
            toFind = AchievementCounterKeys.VirtualCount;
        }
        else
        {
            throw new CustomBasicException("Unknown effect type");
        }
        var plan = _plans.SingleOrDefault(x => x.CounterKey == toFind && x.SourceKey == target);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.AutomationUpgradeProgress.Single(x => x.Counter == toFind && x.Target == target);
        personal.Level = level;
        int coins = CoinsFromExactAchievementTarget(plan, level);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessInstantUnlimited(string item)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.SpecificInstantUnlimited && x.ItemKey == item);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.InstantUnlimitedProgress.Single(x => x.Item == item);
        if (personal.Count == 1)
        {
            throw new CustomBasicException("Should not double count");
        }
        personal.Count = 1;
        if (plan.Target != 1)
        {
            throw new CustomBasicException("Instant unlimited must target only 1");
        }
        if (plan.CoinReward is null)
        {
            throw new CustomBasicException("Instant unlimited must have a coin reward");
        }
        int coinsEarned = plan.CoinReward.Value;
        await ProcessEndAsync(plan, coinsEarned);
    }
    private async void ProcessInventoryAdded(ItemAmount item)
    {
        AchievementPlanModel? plan;
        int earned = 0;
        if (item.Item == CurrencyKeys.Coin)
        {
            //this is coins earned.
            plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CoinEarned);
            if (plan is null)
            {
                return;
            }
            int current = _profileInfo.CoinsEarned;
            int newcount = current + item.Amount;
            earned = CoinsFromSeveralAchievementTargets(plan, current, newcount);
            _profileInfo.CoinsEarned += item.Amount;
            await ProcessEndAsync(plan, earned);
            return;
        }
        plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.UseConsumable && x.ItemKey == item.Item);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.Consumables.Single(x => x.Key == item.Item);
        personal.Count++;
        earned = CoinsFromExactAchievementTarget(plan, personal.Count);
        await ProcessEndAsync(plan, earned);
    }
    private async Task ProcessSuccessfulAchievementAsync(AchievementPlanModel plan, int earned)
    {
        string title = GetPrimaryText(plan);
        OnAchievementSuccessful?.Invoke(title, earned);
        inventoryManager.AddCoin(earned);
        await SaveProfileAsync();
    }
    private async void ProcessInventoryConsumed(ItemAmount item)
    {
        AchievementPlanModel? plan;
        int earned = 0;
        if (item.Item == CurrencyKeys.Coin)
        {
            plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.SpendCoin);
            if (plan is null)
            {
                return;
            }
            int current = _profileInfo.CoinsSpent;
            int newcount = current + item.Amount;
            earned = CoinsFromSeveralAchievementTargets(plan, current, newcount);
            _profileInfo.CoinsSpent += item.Amount;
            await ProcessEndAsync(plan, earned);
            return;
        }
    }
    private async void ProcessWorkshopQueAdded(string buildingName, string craftedItem)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CraftFromWorkshops && x.SourceKey == buildingName && x.ItemKey == craftedItem);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.WorkshopQueued.Single(x => x.BuildingName == buildingName && x.ItemCrafted == craftedItem);
        personal.Count++;
        int coins = CoinsEarnedFromAchievement(personal.Count);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessWorksiteRewards(string location, ItemAmount reward)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.FindFromWorksites && x.SourceKey == location && x.ItemKey == reward.Item);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.WorksiteFoundProgress.Single(x => x.Location == location && x.Item == reward.Item);
        int olds = personal.Count;
        personal.Count += reward.Amount;
        int coins = CoinsFromSeveralAchievementTargets(plan, olds, personal.Count);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessLevelIncrease(int newLevel)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.Level);
        if (plan is null)
        {
            return;
        }
        int coins = CoinsFromExactAchievementTarget(plan, newLevel);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessTimedBoost(string boostKey, string? outputAugmentationKey)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.UseTimedBoost && x.SourceKey == boostKey && x.OutputAugmentationKey == outputAugmentationKey);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.TimedBoostProgress.Single(x => x.SourceKey == boostKey && x.OutputAugmentationKey == outputAugmentationKey);
        personal.Count++;
        int coins = CoinsFromExactAchievementTarget(plan, personal.Count);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessAnimalCollected(string animalName)
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CollectFromAnimal && x.SourceKey == animalName);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.AnimalCollectProgress.Single(x => x.Name == animalName);
        personal.Count++;
        int coins = CoinsFromExactAchievementTarget(plan, personal.Count);
        await ProcessEndAsync(plan, coins);
    }
    private async void ProcessOrderCompleted(string item)
    {
        await ProcessBlankOrderAsync();//this can double count (because you complete one with an order if that matches) plus for completion of any order.
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CompleteOrders && x.ItemKey == item);
        if (plan is null)
        {
            return;
        }
        var personal = _profileInfo.OrderItemProgress.Single(x => x.ItemName == item);
        personal.Count++;
        int coins = CoinsFromExactAchievementTarget(plan, personal.Count);
        await ProcessEndAsync(plan, coins);
    }
    private async Task ProcessEndAsync(AchievementPlanModel plan, int coins)
    {
        if (coins == 0)
        {
            await SaveProfileAsync();
            return;
        }
        await ProcessSuccessfulAchievementAsync(plan, coins);
    }
    private async Task ProcessBlankOrderAsync()
    {
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CompleteOrders && string.IsNullOrWhiteSpace(x.ItemKey));
        if (plan is null)
        {
            return;
        }
        _profileInfo.OrdersCompleted++;
        int coins = CoinsFromExactAchievementTarget(plan, _profileInfo.OrdersCompleted);
        await ProcessEndAsync(plan, coins);
    }

    //because this required a toast, had to do the scenario one even though i did not do the others yet.
    public async Task<int> ScenarioCompletedAsync()
    {
        //this would give credit for this achievement.
        //must at least do this one.
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CompleteScenarios);
        if (plan is null)
        {
            return 0;
        }
        _profileInfo.ScenariosCompleted++;
        await SaveProfileAsync();
        int coins = CoinsFromExactAchievementTarget(plan, _profileInfo.ScenariosCompleted);
        if (coins > 0)
        {
            inventoryManager.AddCoin(coins);
            return coins;
        }
        return 0;
    }
    private async Task SaveProfileAsync()
    {
        await _profileStorage.SaveAsync(_profileInfo);
    }
    public int CoinsEarnedFromAchievement(int amount)
    {
        //i cannot increment the coins earned here (because if i did, then would double count since the counting can still happen even if i was not on the farm).
        var plan = _plans.SingleOrDefault(x => x.CounterKey == AchievementCounterKeys.CoinEarned);
        if (plan is null)
        {
            return 0;
        }
        int current = _profileInfo.CoinsEarned;
        int newcount = current + amount;
        return CoinsFromSeveralAchievementTargets(plan, current, newcount);
    }
    private static int CoinsFromSeveralAchievementTargets(
        AchievementPlanModel achievement,
        int previousCount,
        int currentCount)
    {
        if (currentCount <= previousCount)
        {
            return 0; // nothing new reached
        }

        // NON-REPEATABLE (single target)
        if (achievement.Target is not null)
        {
            if (achievement.CoinReward.HasValue == false)
            {
                throw new CustomBasicException("If there is a target, must have reward to go with it");
            }

            int target = achievement.Target.Value;

            // reward once if we crossed or landed on the target during this update
            return (previousCount < target && target <= currentCount)
                ? achievement.CoinReward.Value
                : 0;
        }

        // REPEATABLE
        if (achievement.RepeatAchievementRules is null || achievement.RepeatRewardRules is null)
        {
            throw new CustomBasicException("Repeatable achievement must have repeat rules and reward rules");
        }

        var targets = achievement.RepeatAchievementRules.FirstTargets;
        var rewards = achievement.RepeatRewardRules.FirstCoinRewards;

        if (targets.Count == 0)
        {
            throw new CustomBasicException("Repeatable achievement must have at least one FirstTarget");
        }
        if (targets.Count != rewards.Count)
        {
            throw new CustomBasicException("Repeat targets and rewards must have same count");
        }

        int total = 0;

        // 1) Sum rewards for explicit tiers that were crossed
        for (int i = 0; i < targets.Count; i++)
        {
            int t = targets[i];
            if (previousCount < t && t <= currentCount)
            {
                total += rewards[i];
            }
        }

        // 2) Sum rewards for repeating tiers AFTER the last explicit one
        int lastTarget = targets[^1];

        // If we never reach beyond lastTarget, we're done
        if (currentCount <= lastTarget)
        {
            return total;
        }

        int increment = achievement.RepeatAchievementRules.IncrementAfterFirst;
        if (increment <= 0)
        {
            throw new CustomBasicException("IncrementAfterFirst must be > 0");
        }

        int fixedReward = achievement.RepeatRewardRules.CoinRewardAfterFirst
            ?? rewards[^1];

        // We need to count tiers: lastTarget + k*increment
        // where tier is in (previousCount, currentCount]
        // Start k at the first tier > previousCount
        int startK;
        if (previousCount < lastTarget)
        {
            startK = 1; // first repeating tier is lastTarget + increment
        }
        else
        {
            int deltaPrev = previousCount - lastTarget;
            // smallest k such that lastTarget + k*increment > previousCount
            startK = (deltaPrev / increment) + 1;
        }

        int deltaCur = currentCount - lastTarget;
        int endK = deltaCur / increment; // largest k such that tier <= currentCount

        if (endK >= startK)
        {
            int count = endK - startK + 1;
            total += count * fixedReward;
        }

        return total;
    }


    private static int CoinsFromExactAchievementTarget(AchievementPlanModel achievement, int currentCount)
    {
        // NON-REPEATABLE
        if (achievement.Target is not null)
        {
            if (achievement.CoinReward.HasValue == false)
            {
                throw new CustomBasicException("If there is a target, must have reward to go with it");
            }
            return achievement.Target.Value == currentCount ? achievement.CoinReward.Value : 0;
        }

        // REPEATABLE
        if (achievement.RepeatAchievementRules is null || achievement.RepeatRewardRules is null)
        {
            throw new CustomBasicException("Repeatable achievement must have repeat rules and reward rules");
        }

        var targets = achievement.RepeatAchievementRules.FirstTargets;
        var rewards = achievement.RepeatRewardRules.FirstCoinRewards;

        if (targets.Count == 0)
        {
            throw new CustomBasicException("Repeatable achievement must have at least one FirstTarget");
        }
        if (targets.Count != rewards.Count)
        {
            throw new CustomBasicException("Repeat targets and rewards must have same count");
        }

        // 1) Exact match against the explicit tiers
        for (int i = 0; i < targets.Count; i++)
        {
            if (currentCount == targets[i])
            {
                return rewards[i];
            }
        }

        // 2) Exact match against repeating tiers AFTER the last explicit one
        int lastTarget = targets[^1];
        if (currentCount <= lastTarget)
        {
            return 0; // (not an exact match for any first tier, and not beyond last tier)
        }

        int increment = achievement.RepeatAchievementRules.IncrementAfterFirst;
        if (increment <= 0)
        {
            throw new CustomBasicException("IncrementAfterFirst must be > 0");
        }

        int delta = currentCount - lastTarget;

        // must land EXACTLY on a tier
        if (delta % increment != 0)
        {
            return 0;
        }

        // Fixed reward for all tiers after the first set
        int fixedReward = achievement.RepeatRewardRules.CoinRewardAfterFirst
            ?? rewards[^1];

        return fixedReward;
    }
    private static string GetPrimaryText(AchievementPlanModel plan)
    {
        return plan.CounterKey switch
        {
            AchievementCounterKeys.Level => "Reach level",
            AchievementCounterKeys.CompleteScenarios => "Complete scenarios",
            AchievementCounterKeys.CompleteOrders when
                string.IsNullOrWhiteSpace(plan.ItemKey) == false
                    => $"Complete {plan.ItemKey.GetWords} orders",

            AchievementCounterKeys.CompleteOrders
                    => "Complete orders",
            AchievementCounterKeys.SpendCoin => "Spend coins",
            AchievementCounterKeys.CoinEarned => "Earn coins",
            AchievementCounterKeys.UseTimedBoost when
                string.IsNullOrWhiteSpace(plan.OutputAugmentationKey) == false
                    => $"Use {plan.OutputAugmentationKey.GetWords} Power Pin",
            AchievementCounterKeys.UseTimedBoost => $"Use timed boost {plan.SourceKey.GetWords}",
            AchievementCounterKeys.UseConsumable => $"Use {plan.ItemKey.GetWords}",
            AchievementCounterKeys.CollectFromAnimal => $"Collect from {plan.SourceKey.GetWords}",
            AchievementCounterKeys.CraftFromWorkshops => $"Craft {plan.ItemKey.GetWords} from {plan.SourceKey.GetWords}",
            AchievementCounterKeys.FindFromWorksites => $"Find {plan.ItemKey.GetWords} from {plan.SourceKey.GetWords}",
            AchievementCounterKeys.SpecificInstantUnlimited => $"Upgrade {plan.ItemKey.GetWords} to Instant Unlimited",
            AchievementCounterKeys.QueUpgrade => $"Purchase {plan.SourceKey.GetWords} Queue Size Upgrade",
            AchievementCounterKeys.ReducedTimeUpgrade => $"Purchase {plan.SourceKey.GetWords} Reduced Time Upgrade",
            AchievementCounterKeys.VirtualCount => $"Purchase {plan.SourceKey.GetWords} Virtual Count Upgrade",
            _ => plan.CounterKey.GetWords
        };
    }


    public BasicList<AchievementUiRow> GetCurrentAchievementsForUi()
    {
        BasicList<AchievementUiRow> output = [];

        foreach (var plan in _plans)
        {
            int current = GetCurrentCount(plan);

            // Non-repeatable
            if (plan.Target is not null)
            {
                int target = plan.Target.Value;
                if (current >= target)
                {
                    continue; // already completed, show on Completed tab instead
                }

                output.Add(new AchievementUiRow
                {
                    Title = GetPrimaryText(plan),
                    Current = current,
                    Target = target,
                    CoinReward = plan.CoinReward ?? 0,
                    IsRepeatable = false
                });
                continue;
            }

            // Repeatable
            if (plan.RepeatAchievementRules is null || plan.RepeatRewardRules is null)
            {
                continue; // defensive - bad plan data
            }

            int nextTarget = GetNextRepeatTarget(plan, current, out int reward);
            output.Add(new AchievementUiRow
            {
                Title = GetPrimaryText(plan),
                Current = current,
                Target = nextTarget,
                CoinReward = reward,
                IsRepeatable = true
            });
        }

        // “Feels good” ordering: most complete first
        return output
            .OrderByDescending(x => x.Percent)
            .ThenBy(x => x.Title)
            .ToBasicList();
    }

    public BasicList<AchievementUiRow> GetCompletedAchievementsForUi()
    {
        BasicList<AchievementUiRow> output = [];

        foreach (var plan in _plans)
        {
            int current = GetCurrentCount(plan);

            // Non-repeatable completed
            if (plan.Target is not null)
            {
                int target = plan.Target.Value;
                if (current < target)
                {
                    continue;
                }

                output.Add(new AchievementUiRow
                {
                    Title = GetPrimaryText(plan),
                    Current = current,
                    Target = target,
                    CoinReward = plan.CoinReward ?? 0,
                    IsRepeatable = false
                });
                continue;
            }

            // Repeatable: show last completed tier only
            if (plan.RepeatAchievementRules is null || plan.RepeatRewardRules is null)
            {
                continue;
            }

            if (TryGetLastRepeatTier(plan, current, out int lastTier, out int reward) == false)
            {
                continue; // none completed yet
            }

            output.Add(new AchievementUiRow
            {
                Title = GetPrimaryText(plan),
                Current = current,
                Target = lastTier,
                CoinReward = reward,
                IsRepeatable = true
            });
        }

        return output
            .OrderBy(x => x.Title)
            .ToBasicList();
    }

    private int GetCurrentCount(AchievementPlanModel plan)
    {
        // Anything missing should just show as 0 in UI (don’t crash the modal)
        return plan.CounterKey switch
        {
            AchievementCounterKeys.Level => progressionManager.CurrentLevel,
            AchievementCounterKeys.CompleteScenarios => _profileInfo.ScenariosCompleted,
            AchievementCounterKeys.CompleteOrders when string.IsNullOrWhiteSpace(plan.ItemKey)
                => _profileInfo.OrdersCompleted,

            AchievementCounterKeys.CompleteOrders
                => _profileInfo.OrderItemProgress
                    .SingleOrDefault(x => x.ItemName == plan.ItemKey)?.Count ?? 0,

            AchievementCounterKeys.SpendCoin => _profileInfo.CoinsSpent,
            AchievementCounterKeys.CoinEarned => _profileInfo.CoinsEarned,

            AchievementCounterKeys.UseConsumable
                => _profileInfo.Consumables
                    .SingleOrDefault(x => x.Key == plan.ItemKey)?.Count ?? 0,

            AchievementCounterKeys.CollectFromAnimal
                => _profileInfo.AnimalCollectProgress
                    .SingleOrDefault(x => x.Name == plan.SourceKey)?.Count ?? 0,

            AchievementCounterKeys.CraftFromWorkshops
                => _profileInfo.WorkshopQueued
                    .SingleOrDefault(x => x.BuildingName == plan.SourceKey && x.ItemCrafted == plan.ItemKey)?.Count ?? 0,

            AchievementCounterKeys.FindFromWorksites
                => _profileInfo.WorksiteFoundProgress
                    .SingleOrDefault(x => x.Location == plan.SourceKey && x.Item == plan.ItemKey)?.Count ?? 0,

            AchievementCounterKeys.UseTimedBoost
                => _profileInfo.TimedBoostProgress
                    .SingleOrDefault(x => x.SourceKey == plan.SourceKey && x.OutputAugmentationKey == plan.OutputAugmentationKey)?.Count ?? 0,
            AchievementCounterKeys.SpecificInstantUnlimited
                => _profileInfo.InstantUnlimitedProgress
                    .SingleOrDefault(x => x.Item == plan.ItemKey)?.Count ?? 0,
            AchievementCounterKeys.QueUpgrade
            or AchievementCounterKeys.VirtualCount
            or AchievementCounterKeys.ReducedTimeUpgrade
                => _profileInfo.AutomationUpgradeProgress
                    .SingleOrDefault(x => x.Counter == plan.CounterKey && x.Target == plan.SourceKey)?.Level ?? 0,

            _ => 0
        };
    }

    private static int GetNextRepeatTarget(AchievementPlanModel plan, int current, out int reward)
    {
        var targets = plan.RepeatAchievementRules!.FirstTargets;
        var rewards = plan.RepeatRewardRules!.FirstCoinRewards;

        // Next explicit tier
        foreach (var t in targets)
        {
            if (current < t)
            {
                reward = rewards[targets.IndexOf(t)];
                return t;
            }
        }

        // After last explicit, repeat using increment
        int lastTarget = targets[^1];
        int increment = plan.RepeatAchievementRules.IncrementAfterFirst;
        int fixedReward = plan.RepeatRewardRules.CoinRewardAfterFirst ?? rewards[^1];

        int delta = Math.Max(0, current - lastTarget);
        int k = (delta / increment) + 1;

        reward = fixedReward;
        return lastTarget + (k * increment);
    }

    private static bool TryGetLastRepeatTier(AchievementPlanModel plan, int current, out int lastTier, out int reward)
    {
        var targets = plan.RepeatAchievementRules!.FirstTargets;
        var rewards = plan.RepeatRewardRules!.FirstCoinRewards;

        // If we haven't hit the first tier, nothing completed
        if (current < targets[0])
        {
            lastTier = 0;
            reward = 0;
            return false;
        }

        // If within explicit list, last completed is greatest target <= current
        int explicitLast = 0;
        int explicitReward = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] <= current)
            {
                explicitLast = targets[i];
                explicitReward = rewards[i];
            }
        }

        int lastExplicitTarget = targets[^1];
        if (current <= lastExplicitTarget)
        {
            lastTier = explicitLast;
            reward = explicitReward;
            return true;
        }

        // Past explicit list: compute last repeating tier <= current
        int increment = plan.RepeatAchievementRules.IncrementAfterFirst;
        int fixedReward = plan.RepeatRewardRules.CoinRewardAfterFirst ?? rewards[^1];

        int delta = current - lastExplicitTarget;
        int k = delta / increment; // floor => last tier hit

        lastTier = lastExplicitTarget + (k * increment);
        reward = fixedReward;
        return true;
    }


}
namespace FarmSim.Domain.Services.OutputAugmentation;
public class OutputAugmentationManager
{
    BasicList<OutputAugmentationPlanModel> _plans = [];
    public async Task SetOutputAugmentationStyleContextAsync(OutputAugmentationServicesContext context, FarmKey farm)
    {
        _plans = await context.OutputAugmentationPlanProvider.GetPlanAsync(farm);
    }
    //i propose that instead of getting the plan directly, will simply give the details from it.
    private OutputAugmentationPlanModel GetPlanModel(string key)
    {
        var plan = _plans.SingleOrDefault(x => x.Key == key) ?? throw new CustomBasicException($"Could not find output augmentation plan with key {key}");
        return plan;
    }


    public OutputAugmentationSnapshot GetSnapshot(string key)
    {
        var plan = GetPlanModel(key);
        return new OutputAugmentationSnapshot
        {
            IsDouble = plan.IsDouble,
            ExtraRewards = plan.Rewards.ToBasicList(),
            Chance = plan.ChancePercent
        };
    }

    private static bool SameItem(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string FormatRewardList(IEnumerable<string> rewards)
    {
        var list = rewards
            .Where(x => string.IsNullOrWhiteSpace(x) == false)
            .Select(x => x.GetWords)
            .ToList();

        if (list.Count == 0)
        {
            return "extra rewards";
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        if (list.Count == 2)
        {
            return $"{list[0]} and {list[1]}";
        }

        return $"{string.Join(", ", list.Take(list.Count - 1))}, and {list.Last()}";
    }
    public string GetDescription(string key)
    {
        var plan = GetPlanModel(key);

        string target = plan.TargetName.GetWords;

        var rewards = plan.Rewards ?? [];
        if (rewards.Count == 0)
        {
            throw new CustomBasicException("Must be at least one reward");
        }
        double chance = plan.ChancePercent;

        // normalize
        if (chance < 0)
        {
            chance = 0;
        }

        if (chance > 100)
        {
            chance = 100;
        }

        // RULE 1: Double output
        if (plan.IsDouble)
        {
            if (chance < 100)
            {
                throw new CustomBasicException("Must be 100% chance for double output plans.");
            }
            return $"Receive double {FormatRewardList(rewards)} when {plan.TargetName} finishes.";
            
        }

        // RULE 2: Guaranteed extra list (100% and more than 1 reward)
        if (rewards.Count > 1)
        {
            if (chance < 100)
            {
                throw new CustomBasicException("Must be 100% chance for multiple reward plans.");
            }
            return $"When {target} finishes, you will also receive {FormatRewardList(rewards)}.";
        }

        // Guaranteed single reward (100%)
        if (chance == 100)
        {
            if (rewards.Count == 0)
            {
                return $"When {target} finishes, you will receive extra rewards.";
            }

            // Special: single reward equals target => extra of target
            if (rewards.Count == 1 && SameItem(rewards[0], plan.TargetName))
            {
                return $"When {target} finishes, you will receive an extra {target}.";
            }

            return $"When {target} finishes, you will also receive {FormatRewardList(rewards)}.";
        }

        

        // Special wording: only one reward and it matches the target
        if (rewards.Count == 1 && SameItem(rewards[0], plan.TargetName))
        {
            return $"Chance to receive an extra {target} when it finishes.";
        }

        // Otherwise: chance to receive reward(s) different than target
        return $"Chance to also receive {FormatRewardList(rewards)} when {target} finishes.";

        //not sure how the others will work yet.
    }

}
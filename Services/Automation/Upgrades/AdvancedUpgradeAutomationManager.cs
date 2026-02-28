namespace FarmSim.Domain.Services.Automation.Upgrades;
public class AdvancedUpgradeAutomationManager(InventoryManager inventoryManager, Func<string, Task> upgradeToInstantUnlimitedAsync)
{
    private BasicList<AdvancedUpgradeAutomationPlanModel> _plans = [];
    private BasicList<AdvancedUpgradeAutomationOwnedModel> _ownList = [];
    private IAdvancedUpgradeAutomationProfile _profile = null!;
    public event Action<string, EnumAdvancedUpgradeAutomationEffect, int>? OnUpgradePurchased; //target, effect, new level
    public async Task SetStyleContextAsync(UpgradeAutomationServicesContext context, FarmKey farm)
    {
        _plans = await context.AdvancedUpgradeAutomationPlanProvider.GetPlansAsync(farm);
        _profile = context.AdvancedUpgradeAutomationProfile;
        _ownList = await _profile.LoadAsync();
        ValidatePlans();
    }
    private void ValidatePlans()
    {
        foreach (var plan in _plans)
        {
            foreach (var track in plan.Tracks)
            {
                if (track.Effect == EnumAdvancedUpgradeAutomationEffect.QueueSize && track.IsUnlimited)
                {
                    if (track.Ladder.ValuePerLevelAfterMax is null && track.Ladder.ClampToLast == false)
                    {
                        throw new CustomBasicException($"Unlimited QueueSize track '{plan.Target}' must define ValuePerLevelAfterMax (or ClampToLast).");
                    }
                    if (track.IsUnlimited && track.Ladder.CostPerLevelAfterMax is null)
                    {
                        throw new CustomBasicException($"Unlimited track '{plan.Target}' / {track.Effect} must define CostPerLevelAfterMax.");
                    }
                }
            }
        }
    }
    private bool HasTrack(string target, EnumAdvancedUpgradeAutomationEffect effect)
    {
        var plan = _plans.Single(x => x.Target == target);
        return plan.Tracks.Any(x => x.Effect == effect);
    }
    private AdvancedUpgradeAutomationTrack? GetTrack(string target, EnumAdvancedUpgradeAutomationEffect effect)
    {
        var plan = _plans.Single(x => x.Target == target);
        return plan.Tracks.SingleOrDefault(x => x.Effect == effect);
    }
    private static int GetSum(LevelLadderDto ladder, int level)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        if (level <= 0)
        {
            return 0;
        }
        var values = ladder.ValueByLevel;
        if (values is null || values.Count == 0)
        {
            if (ladder.ValuePerLevelAfterMax is int perOnly)
            {
                return level * perOnly;
            }

            return 0;
        }

        int tableCount = values.Count;

        // Case 1: Entirely inside the defined table
        if (level <= tableCount)
        {
            int sum = 0;
            for (int i = 0; i < level; i++)
            {
                sum += values[i];
            }
            return sum;
        }

        // Case 2: Past the defined table
        int total = 0;

        // Sum entire table first
        for (int i = 0; i < tableCount; i++)
        {
            total += values[i];
        }

        int extraLevels = level - tableCount;

        if (ladder.ValuePerLevelAfterMax is int perAfter)
        {
            total += extraLevels * perAfter;
        }
        else if (ladder.ClampToLast)
        {
            total += extraLevels * values[^1];
        }
        return total;
    }
    private int GetOwnedLevel(string target, EnumAdvancedUpgradeAutomationEffect effect)
        => _ownList.SingleOrDefault(x => x.Target == target && x.Effect == effect)?.Level ?? 0;
    public int ExtraVirtualCountBenefit(string target)
    {
        int level = GetOwnedLevel(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount);
        var track = GetTrack(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount);
        if (track is null)
        {
            return 0;
        }
        if (track.IsUnlimited)
        {
            throw new CustomBasicException("Should not have unlimited virtual counts");
        }
        return GetSum(track.Ladder, level);
    }
    public int ExtraQueCountBenefit(string target)
    {
        int level = GetOwnedLevel(target, EnumAdvancedUpgradeAutomationEffect.QueueSize);
        var track = GetTrack(target, EnumAdvancedUpgradeAutomationEffect.QueueSize);
        if (track is null)
        {
            return 0;
        }
        return GetSum(track.Ladder, level);
    }
    public double? GetTimeReductionBenefit(string target)
    {
        int level = GetOwnedLevel(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent);
        var track = GetTrack(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent);
        if (track is null)
        {
            return null;
        }
        if (track.IsUnlimited)
        {
            throw new CustomBasicException("Should not have unlimited time reductions");
        }
        if (level == 0)
        {
            return null; //expects null value (not 0).
        }
        int requested = track.Ladder.ValueByLevel[level - 1];
        return requested * 0.01;
    }

    private AdvancedUpgradeAutomationDetailModel GetDetails(string target, EnumAdvancedUpgradeAutomationEffect effect)
    {
        int ownedLevel = GetOwnedLevel(target, effect); // 0 = none owned yet
        var track = GetTrack(target, effect) ?? throw new CustomBasicException($"No details found for {target} and effect of {effect}");
        //int nextLevel = ownedLevel + 1;

        // Fully upgraded only applies to NON-unlimited tracks
        if (track.IsUnlimited == false && track.MaxLevel > 0 && ownedLevel >= track.MaxLevel)
        {
            return new()
            {
                CostAmount = 0,
                Currency = track.CostCurrencyKey,
                Level = ownedLevel,
                MaximumLevels = track.MaxLevel,
                Effect = effect
            };
        }

        // Table-driven cost: cost for next level is CostByLevel[ownedLevel]
        var costs = track.Ladder.CostByLevel;
        int? maxLevel = null;
        if (track.IsUnlimited == false)
        {
            maxLevel = track.MaxLevel;
        }
        BasicList<int> times = [];
        if (effect == EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent)
        {
            times = track.Ladder.ValueByLevel.ToBasicList();
        }
        if (costs is not null && ownedLevel < costs.Count)
        {
            return new()
            {
                CostAmount = costs[ownedLevel],   // ownedLevel 0 -> first cost
                Currency = track.CostCurrencyKey,
                Level = ownedLevel,
                MaximumLevels = maxLevel,
                NextBenefit = track.Ladder.ValueByLevel[ownedLevel],
                Effect = effect,
                ReducedTimes = times
            };
        }

        // Past the table: use per-level cost (typically for unlimited / after-max behavior)
        if (track.Ladder.CostPerLevelAfterMax.HasValue == false)
        {
            throw new CustomBasicException(
                $"Missing CostPerLevelAfterMax for '{target}' effect '{effect}' at level {ownedLevel}.");
        }
        if (track.Ladder.ValuePerLevelAfterMax.HasValue == false)
        {
            throw new CustomBasicException(
                $"Missing ValuePerLevelAfterMax for '{target}' effect '{effect}' at level {ownedLevel}.");
        }
        return new()
        {
            CostAmount = track.Ladder.CostPerLevelAfterMax.Value,
            Currency = track.CostCurrencyKey,
            Level = ownedLevel,
            MaximumLevels = track.MaxLevel,
            NextBenefit = track.Ladder.ValuePerLevelAfterMax.Value
        };
    }
    public AdvancedUpgradeAutomationDetailModel GetTimeReductionUpgradeCostDetails(string target)
    {
        if (HasTrack(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent) == false)
        {
            return new();
        }
        return GetDetails(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent);
    }
    public AdvancedUpgradeAutomationDetailModel GetVirtualUpgradeCostDetails(string target)
    {
        if (HasTrack(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount) == false)
        {
            return new();
        }
        return GetDetails(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount);
    }
    public AdvancedUpgradeAutomationDetailModel GetQueUpgradeCostDetails(string target)
    {
        if (HasTrack(target, EnumAdvancedUpgradeAutomationEffect.QueueSize) == false)
        {
            return new();
        }
        return GetDetails(target, EnumAdvancedUpgradeAutomationEffect.QueueSize);
    }
    private void Validate(string target, AdvancedUpgradeAutomationDetailModel details)
    {
        if (details.CostAmount == 0)
        {
            throw new CustomBasicException($"{target} was fully upgraded");
        }
        if (inventoryManager.Has(details.Currency, details.CostAmount) == false)
        {
            throw new CustomBasicException($"Don't have enough {details.Currency} to purchase upgrade");
        }
    }
    private async Task UpsertEntryAsync(string target, EnumAdvancedUpgradeAutomationEffect effect)
    {
        var owned = _ownList.SingleOrDefault(x => x.Target == target && x.Effect == effect);
        if (owned is null)
        {
            owned = new()
            {
                Effect = effect,
                Target = target,
                Level = 1
            };
            _ownList.Add(owned);
            await _profile.SaveAsync(_ownList);
            return;
        }
        owned.Level++;
        await _profile.SaveAsync(_ownList);
    }

    public async Task PurchaseVirtualCountUpgradeAsync(string target)
    {
        var details = GetDetails(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount);
        Validate(target, details);
        await UpsertEntryAsync(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount);
        await PossibleInstantUnlimitedAsync(target);
        OnUpgradePurchased?.Invoke(target, EnumAdvancedUpgradeAutomationEffect.VirtualCount, details.Level + 1);
        inventoryManager.Consume(details.Currency, details.CostAmount);
    }

    public async Task PurchaseQueUpgradeAsync(string target)
    {
        var details = GetDetails(target, EnumAdvancedUpgradeAutomationEffect.QueueSize);
        Validate(target, details);
        await UpsertEntryAsync(target, EnumAdvancedUpgradeAutomationEffect.QueueSize);
        await PossibleInstantUnlimitedAsync(target);
        OnUpgradePurchased?.Invoke(target, EnumAdvancedUpgradeAutomationEffect.QueueSize, details.Level + 1);
        inventoryManager.Consume(details.Currency, details.CostAmount);
    }
    public async Task PurchaseTimeReductionUpgradeAsync(string target)
    {
        var details = GetDetails(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent);
        Validate(target, details);
        await UpsertEntryAsync(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent);
        await PossibleInstantUnlimitedAsync(target);
        OnUpgradePurchased?.Invoke(target, EnumAdvancedUpgradeAutomationEffect.ReducedTimePercent, details.Level + 1);
        inventoryManager.Consume(details.Currency, details.CostAmount);
    }
    private async Task PossibleInstantUnlimitedAsync(string target)
    {
        var plan = _plans.Single(x => x.Target == target);

        // If any track is unlimited, then this target can never become instant unlimited
        if (plan.Tracks.Any(t => t.IsUnlimited))
        {
            return;
        }

        // Pull owned upgrades for this target, keyed by effect (avoid count-based logic)
        var ownsByEffect = _ownList
            .Where(o => o.Target == target)
            .GroupBy(o => o.Effect)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Level)); // or Single if guaranteed unique

        // Must have an owned entry for EVERY track in the plan
        foreach (var track in plan.Tracks)
        {
            if (ownsByEffect.TryGetValue(track.Effect, out int ownedLevel) == false)
            {
                return; // missing a track entirely
            }

            int max = track.MaxLevel;
            if (max <= 0)
            {
                throw new CustomBasicException(
                    $"Target '{target}' track '{track.Effect}' must have MaxLevel > 0 to qualify for instant unlimited.");
            }

            if (ownedLevel < max)
            {
                return; // not fully upgraded yet
            }
        }
        if (upgradeToInstantUnlimitedAsync is null)
        {
            throw new CustomBasicException("Nothing upgrading to instant unlimited");
        }
        await upgradeToInstantUnlimitedAsync.Invoke(target);
        //await instantUnlimitedManager.UpgradeToInstantUnlimitedAsync(target);
    }
}
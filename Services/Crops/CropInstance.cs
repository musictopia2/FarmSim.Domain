namespace FarmSim.Domain.Services.Crops;
public class CropInstance(double currentMultiplier,
    CropRecipe? currentRecipe,
    TimedBoostManager timedBoostManager, OutputAugmentationManager outputAugmentationManager
    )
{
    public Guid Id { get; } = Guid.NewGuid(); // unique per instance
    public bool Unlocked { get; set; }
    public EnumCropState State { get; private set; } = EnumCropState.Empty;
    public DateTime? PlantedAt { get; private set; }
    public string? Crop { get; private set; } = null;
    public TimeSpan? GrowTime { get; private set; } = null;
    public double? AdvancedSpeedBonus { get ; private set; } = null;
    public bool? MaxedBenefits { get; private set; } = null;
    public OutputAugmentationSnapshot? OutputPromise { get; private set; }
    public BasicList<ItemAmount> ExtraRewards { get; private set; } = [];
    private bool _needsSaving;
    private bool _extrasResolved;
    public bool NeedsToSave => _needsSaving;
    // NEW: separate the two meanings
    private readonly double _currentMultiplier = TimingRules.ValidateMultiplier(currentMultiplier);
    private double? _runMultiplier; // locked per run; null when idle
    public TimeSpan ReducedBy { get; private set; } = TimeSpan.Zero;

    public bool IsExtrasResolved => _extrasResolved;

    public TimeSpan? ReadyTime
    {
        get
        {
            if (State != EnumCropState.Growing || PlantedAt is null)
            {
                return null;
            }
            var elapsed = DateTime.Now - PlantedAt.Value;
            var remaining = GrowTime - elapsed;
            return remaining > TimeSpan.Zero
                ? remaining
                : TimeSpan.Zero;
        }
    }

    private TimeSpan? GetDuration
    {
        get
        {
            if (currentRecipe is null)
            {
                return null;
            }

            var baseMult = _runMultiplier ?? _currentMultiplier;

            // If you want instant only for “fast” crops with max benefits,
            // CropInstance needs to know that. Easiest: compute it from recipe + a flag you store.
            bool canInstant = currentRecipe.IsFast && (MaxedBenefits ?? false);

            double bonusMult = AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);

            TimeSpan duration = currentRecipe.Duration - ReducedBy;

            return duration.Apply(baseMult * bonusMult, canInstant);
        }
    }
    public void Load(CropAutoResumeModel slot)
    {
        Crop = slot.Crop;
        State = slot.State;
        PlantedAt = slot.PlantedAt;
        Unlocked = slot.Unlocked;
        ReducedBy = slot.ReducedBy;
        _extrasResolved = slot.ExtrasResolved;
        OutputPromise = slot.OutputPromise;
        ExtraRewards = slot.ExtraRewards;
        MaxedBenefits = slot.MaxedBenefits;
        AdvancedSpeedBonus = slot.AdvancedSpeedBonus;
        _runMultiplier = slot.RunMultiplier;
        // If something is/was planted, ensure a run multiplier exists
        if (Crop is not null && _runMultiplier is null)
        {
            _runMultiplier = _currentMultiplier;
        }
        GrowTime = Crop is null ? null : GetDuration;
    }
    public void Plant(string crop, CropRecipe recipe, TimeSpan reducedBy, bool maxed, double? speedBonus)
    {
        State = EnumCropState.Growing;
        currentRecipe = recipe;
        ReducedBy = reducedBy;
        MaxedBenefits = maxed;
        _runMultiplier = _currentMultiplier;
        AdvancedSpeedBonus = speedBonus;
        GrowTime = GetDuration; //must send the recipe now.  can't trust the time sent anymore.
        Crop = crop;
        PlantedAt = DateTime.Now;
        OutputPromise = null; //clear out any old promises.
        _extrasResolved = false;
        ExtraRewards.Clear();
        RunPossibleAugmentation();
    }

    private void RunPossibleAugmentation()
    {
        if (OutputPromise is not null)
        {
            return; //already promised.
        }
        if (Crop is null)
        {
            return; //i think.
        }
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(Crop);
        if (key is null)
        {
            return;
        }
        OutputPromise = outputAugmentationManager.GetSnapshot(key);
        _needsSaving = true;
    }

    public void UpdateTick()
    {
        _needsSaving = false;
        if (State == EnumCropState.Ready && OutputPromise is null)
        {
            RunPossibleAugmentation();
        }
        if (State == EnumCropState.Ready && OutputPromise is not null && _extrasResolved == false)
        {
            ResolveExtraRewards();
            _extrasResolved = true;
            _needsSaving = true;
        }
        if (State != EnumCropState.Growing || PlantedAt == null)
        {
            return;
        }
        var elapsed = DateTime.Now - PlantedAt.Value;
        RunPossibleAugmentation();
        if (elapsed >= GrowTime)
        {
            State = EnumCropState.Ready;
            PlantedAt = null;
            if (OutputPromise is not null)
            {
                ResolveExtraRewards();
                _extrasResolved = true;
            }
            else
            {
                _extrasResolved = false;
            }
            _needsSaving = true;
        }
    }
    private void ResolveExtraRewards()
    {
        if (OutputPromise is null)
        {
            return;
        }
        ExtraRewards.Clear();
        if (OutputPromise.IsDouble)
        {
            throw new CustomBasicException("I don't think that crops can support doubles");
        }
        if (OutputPromise.Chance >= 100)
        {
            throw new CustomBasicException("I don't think that crops can support guaranteed extras");
        }
        if (OutputPromise.ExtraRewards.Count > 1)
        {
            throw new CustomBasicException("Crops cannot have multiple extra rewards at this time");
        }
        bool hit = rs1.RollHit(OutputPromise.Chance);

        //Console.WriteLine($"{hit} for chance of {OutputPromise.Chance}");
        if (hit == false)
        {
            return; //you did not get anything extra.
        }
        ExtraRewards.Add(new()
        {
            Item = OutputPromise.ExtraRewards.Single(),
            Amount = 1 //always just one for crops.
        });
    }
    public double? GetCurrentRun => currentRecipe is null ? null : _runMultiplier;
    public void Clear()
    {
        State = EnumCropState.Empty;
        ReducedBy = TimeSpan.Zero;
        Crop = null;
        ExtraRewards.Clear();
        _extrasResolved = false;
        OutputPromise = null;
        GrowTime = null;
        PlantedAt = null;
        _runMultiplier = null;
        currentRecipe = null; //don't know anymore the recipe until sent again.
    }
}
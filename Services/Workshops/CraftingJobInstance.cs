namespace FarmSim.Domain.Services.Workshops;
public class CraftingJobInstance(
    WorkshopRecipe recipe,
    double currentMultiplier,
    TimeSpan reducedBy,
    double? extraSpeed,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager
    )
{
    public WorkshopRecipe Recipe => recipe;
    public Guid Id { get; set; } = Guid.NewGuid();
    public EnumWorkshopState State { get; private set; } = EnumWorkshopState.Waiting;
    public TimeSpan ReducedBy { get; } = reducedBy;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public OutputAugmentationSnapshot? OutputPromise { get; private set; } //may be here (?)
    public bool RunPossibleAugmentation()
    {
        if (OutputPromise is not null)
        {
            return false;
        }
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(recipe.Output.Item);
        if (key is null)
        {
            return false;
        }
        OutputPromise = outputAugmentationManager.GetSnapshot(key);
        return true;
    }

    // Current multiplier (not persisted)
    private readonly double _currentMultiplier = TimingRules.ValidateMultiplier(currentMultiplier);

    // Locked promise for THIS job run (persisted while job exists)
    private double? _runMultiplier;

    // Use locked multiplier if present; otherwise current (for previews)
    private TimeSpan EffectiveDuration
    {
        get
        {
            var m = _runMultiplier ?? _currentMultiplier;

            TimeSpan duration = recipe.Duration - ReducedBy;

            double bonusMult = extraSpeed.SpeedBonusToTimeMultiplier();
            return duration.Apply(m * bonusMult); //hopefully this simple this time.
        }
    }

    public TimeSpan DurationForProcessing => EffectiveDuration;

    public bool IsComplete =>
        State == EnumWorkshopState.Active &&
        StartedAt is not null &&
        DateTime.Now - StartedAt.Value >= EffectiveDuration;

    public void Start()
    {
        // Lock promise at job start
        _runMultiplier = _currentMultiplier;
        StartedAt = DateTime.Now;
        CompletedAt = null;
        State = EnumWorkshopState.Active;
    }

    public void Load(CraftingAutoResumeModel craft)
    {
        State = craft.State;
        StartedAt = craft.StartedAt;
        CompletedAt = craft.CompletedAt;
        OutputPromise = craft.OutputPromise;
        _runMultiplier = craft.RunMultiplier;
        // Back-compat / safety:
        // if job exists and has started but run multiplier missing, use current
        if (StartedAt is not null && _runMultiplier is null)
        {
            _runMultiplier = _currentMultiplier;
        }
    }

    public void Complete()
    {
        CompletedAt = DateTime.Now;
        OutputPromise = null;
    }

    public void ReadyForManualPickup()
    {
        CompletedAt = DateTime.Now;
        State = EnumWorkshopState.ReadyToPickUpManually;
    }

    public void UpdateStartedAt(DateTime startedAt)
    {
        // If something external sets the start time, ensure we still have a promise
        _runMultiplier ??= _currentMultiplier;

        StartedAt = startedAt;
        CompletedAt = null;
        State = EnumWorkshopState.Active;
    }

    public TimeSpan? ReadyTime
    {
        get
        {
            if (State != EnumWorkshopState.Active || StartedAt is null)
            {
                return null;
            }

            var elapsed = DateTime.Now - StartedAt.Value;
            var remaining = EffectiveDuration - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public CraftingAutoResumeModel GetCraftingForSaving
    {
        get
        {
            // Persist the promise whenever the job exists / is in queue.
            // If you delete completed jobs from the queue, this still works.
            return new()
            {
                CompletedAt = CompletedAt,
                RecipeItem = recipe.Item,
                StartedAt = StartedAt,
                State = State,
                OutputPromise = OutputPromise,
                // Save locked multiplier if job was ever started; otherwise null is fine
                RunMultiplier = StartedAt is null ? null : _runMultiplier
            };
        }
    }
}
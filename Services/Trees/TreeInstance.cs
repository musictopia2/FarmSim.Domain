namespace FarmSim.Domain.Services.Trees;
public class TreeInstance(
    TreeRecipe tree,
    ITreesCollecting collecting,
    double currentMultiplier,
    TimedBoostManager timedBoostManager,
    OutputAugmentationManager outputAugmentationManager
)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool Unlocked { get; set; }

    public string Name => tree.Item;
    public string TreeName => tree.TreeName;
    public bool IsSuppressed { get; set; } = false;
    public int TreesReady { get; private set; } = collecting.TreesCollectedAtTime;
    public EnumTreeState State { get; private set; } = EnumTreeState.Collecting;
    public TimeSpan ReducedBy { get; private set; } = TimeSpan.Zero;
    public double? AdvancedSpeedBonus { get; set; }
    public int Level { get; set; } = 1;
    public bool MaxBenefits { get; set; }
    private TimeSpan ProductionTimePerTree => tree.ProductionTimeForEach;
    public bool RentalExpired { get; set; }
    public bool IsRental { get; set; }

    // production start time (used mostly for UI/pause semantics)
    private DateTime? StartedAt { get; set; }

    // production clock for per-tree accumulation
    private DateTime? TempStart { get; set; }

    public OutputAugmentationSnapshot? OutputPromise { get; private set; }

    private bool IsCollecting { get; set; } = false;

    private readonly double _currentMultiplier = TimingRules.ValidateMultiplier(currentMultiplier);
    private double? _runMultiplier; // locked per production batch; null when not producing

    // Apply multiplier to time per tree
    private TimeSpan ProductionTimePerTreeAdjusted
    {
        get
        {
            var baseM = _runMultiplier ?? _currentMultiplier;

            // same rule as crops: 0.15 => 15% faster => time multiplier 0.85
            bool canInstant = MaxBenefits && tree.IsFast; // optional; only matters if you support instant trees
            double bonusM = AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);

            double m = baseM * bonusM;

            return ProductionTimePerTree.ApplyWithMinTotalForBatch(
                m,
                collecting.TreesCollectedAtTime,
                ReducedBy, canInstant);
        }
    }
    public TimeSpan BaseTime => ProductionTimePerTree;

    public TimeSpan? ReadyTime
    {
        get
        {
            if (State != EnumTreeState.Producing || TempStart is null)
            {
                return null;
            }

            var elapsed = DateTime.Now - TempStart.Value;

            var perTree = ProductionTimePerTreeAdjusted;
            var totalTime = TimeSpan.FromTicks(perTree.Ticks * collecting.TreesCollectedAtTime);

            var remaining = totalTime - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
    public bool CanCollectOneTree => TreesReady > 0;
    private void StartCollecting()
    {
        if (State == EnumTreeState.Collecting && !IsCollecting)
        {
            IsCollecting = true;
            StartedAt = null; // pause timer
        }
    }
    public TreeAutoResumeModel GetTreeForSaving
    {
        get
        {
            return new()
            {
                StartedAt = StartedAt,
                TempStart = TempStart,
                State = State,
                TreeName = TreeName,
                TreesReady = TreesReady,
                Unlocked = Unlocked,
                IsSuppressed = IsSuppressed,
                ReducedBy = ReducedBy,
                OutputPromise = OutputPromise,
                RentalExpired = RentalExpired,
                IsRental = IsRental,
                AdvancedSpeedBonus = AdvancedSpeedBonus,
                MaxBenefits = MaxBenefits,
                Level = Level,
                Id = Id,
                // Save the promise ONLY while producing
                RunMultiplier = State == EnumTreeState.Producing ? _runMultiplier : null
            };
        }
    }

    public void Load(TreeAutoResumeModel model)
    {
        State = model.State;
        TempStart = model.TempStart;
        Unlocked = model.Unlocked;
        TreesReady = model.TreesReady;
        StartedAt = model.StartedAt;
        IsSuppressed |= model.IsSuppressed;
        ReducedBy = model.ReducedBy;
        _runMultiplier = model.RunMultiplier;
        OutputPromise = model.OutputPromise;
        RentalExpired = model.RentalExpired;
        IsRental = model.IsRental;
        AdvancedSpeedBonus = model.AdvancedSpeedBonus;
        MaxBenefits = model.MaxBenefits;
        Level = model.Level;
        Id = model.Id;
        // Back-compat / safety: if producing but multiplier missing, fall back to current
        if (State == EnumTreeState.Producing && _runMultiplier is null)
        {
            _runMultiplier = _currentMultiplier;
        }
    }
    public void CollectTree(TimeSpan reducedBy)
    {
        StartCollecting();
        if (TreesReady <= 0)
        {
            return;
        }
        TreesReady--;
        if (TreesReady == 0)
        {
            if (RentalExpired)
            {
                Unlocked = false;
            }
            Reset();
            ReducedBy = reducedBy;
            // Start production for next batch

        }
    }

    public void Reset()
    {
        _runMultiplier = _currentMultiplier; // LOCK promise
        State = EnumTreeState.Producing;
        IsCollecting = false;
        ReducedBy = TimeSpan.Zero; //may change later.
        // both clocks start now
        StartedAt = DateTime.Now;
        TempStart = DateTime.Now;
        OutputPromise = null; // clear promise to allow new one
        RunPossibleAugmentation();
        _needsSaving = true;
    }


    private bool _needsSaving;
    public bool NeedsToSave => _needsSaving;
    private void RunPossibleAugmentation()
    {
        if (OutputPromise is not null)
        {
            return; //already promised.
        }
        
        string? key = timedBoostManager.GetActiveOutputAugmentationKeyForItem(Name);
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
        if (State == EnumTreeState.Collecting && OutputPromise is null)
        {
            RunPossibleAugmentation();
        }
        if (State != EnumTreeState.Producing || TempStart is null)
        {
            return;
        }
        RunPossibleAugmentation(); //must run here just in case.
        // Ensure run multiplier exists if something started producing without it (defensive)
        _runMultiplier ??= _currentMultiplier;

        var elapsed = DateTime.Now - TempStart.Value;

        var perTree = ProductionTimePerTreeAdjusted;
        var perTreeSeconds = perTree.TotalSeconds;

        if (perTreeSeconds <= 0)
        {
            // should never happen if recipes are valid, but avoid divide-by-zero
            throw new CustomBasicException("Invalid per-tree production time (<= 0 seconds).");
        }

        // How many full trees should have been produced
        int produced = (int)(elapsed.TotalSeconds / perTreeSeconds);

        if (produced <= 0)
        {
            return;
        }

        TreesReady += produced;
        _needsSaving = true;

        if (TreesReady >= collecting.TreesCollectedAtTime)
        {
            TreesReady = collecting.TreesCollectedAtTime;
            State = EnumTreeState.Collecting;

            StartedAt = null;
            TempStart = null;

            // Promise no longer needed once batch is done
            _runMultiplier = null;
        }
        else
        {
            // Advance TempStart by the amount of time used for produced trees
            TempStart = TempStart.Value.AddSeconds(produced * perTreeSeconds);
        }
    }
}
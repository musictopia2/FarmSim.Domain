namespace FarmSim.Domain.Services.Animals;
public class AnimalInstance(AnimalRecipe recipe, double currentMultiplier,
    TimedBoostManager timedBoostManager, OutputAugmentationManager outputAugmentationManager
    )
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool Unlocked { get; set; } = true;
    public TimeSpan ReducedBy { get; private set; } = TimeSpan.Zero;
    public bool IsSuppressed { get; set; } = false;
    public bool IsRental { get; set; } //this means if it comes from rental, needs to mark so can lock the exact proper one.
    public OutputAugmentationSnapshot? OutputPromise { get; private set; }
    public BasicList<ItemAmount> ExtraRewards { get; private set; } = [];
    public double? AdvancedSpeedBonus { get; set; } //has to set right away now.
    public int Level { get; set; } = 1;
    public bool MaxBenefits { get; set; }
    public bool IsFast => recipe.IsFast;
    public BasicList<AnimalProductionOption> GetUnlockedProductionOptions()
        => recipe.Options.Take(ProductionOptionsAllowed).ToBasicList();
    public int ProductionOptionsAllowed { get; set; }
    public int TotalProductionOptions => recipe.Options.Count;
    public AnimalProductionOption NextProductionOption
    {
        get
        {
            var options = recipe.Options;
            if (options is null || options.Count == 0)
            {
                throw new CustomBasicException($"Animal recipe '{recipe.Animal}' has no production options.");
            }
            int index = Math.Min(ProductionOptionsAllowed, options.Count - 1);
            return options[index];
        }
    }
    public string Name => recipe.Animal;
    public string ItemReceived(int selected) => recipe.Options[selected].Output.Item;

    public int OutputReady { get; private set; } = 0;
    public EnumAnimalState State { get; set; } = EnumAnimalState.None;

    public TimeSpan? Duration { get; private set; }
    public DateTime? StartedAt { get; private set; }

    private int? _selected;

    // NEW: separate the two meanings
    private readonly double _currentMultiplier = TimingRules.ValidateMultiplier(currentMultiplier);
    private double? _runMultiplier; // locked per run; null when idle


    //if i need rebalancing, rethink then.  not until then.
    public void UpdateReady(int amount)
    {
        OutputReady = amount;
        _selected = 0; //must choose this as well.
    }
    public void Load(AnimalAutoResumeModel animal)
    {

        State = animal.State;
        OutputReady = animal.OutputReady;
        StartedAt = animal.StartedAt;
        ProductionOptionsAllowed = animal.ProductionOptionsAllowed;
        Unlocked = animal.Unlocked;
        _selected = animal.Selected;
        ReducedBy = animal.ReducedBy;
        ExtraRewards = animal.ExtraRewards;
        OutputPromise = animal.OutputPromise;
        IsSuppressed = animal.IsSuppressed;
        AdvancedSpeedBonus = animal.AdvancedSpeedBonus;
        Level = animal.Level;
        _extrasResolved = animal.ExtrasResolved;
        MaxBenefits = animal.MaxBenefits;
        IsRental = animal.IsRental;
        // Restore locked promise only (do NOT overwrite current multiplier)
        _runMultiplier = animal.RunMultiplier;

        // Back-compat / safety: if something is in-progress but no run multiplier was saved
        if (_selected is not null && _runMultiplier is null)
        {
            _runMultiplier = _currentMultiplier;
        }

        if (_selected is not null)
        {
            Duration = GetDuration(_selected.Value, ReducedBy);
        }
        else
        {
            Duration = null;
        }
    }
    public AnimalAutoResumeModel GetAnimalForSaving
    {
        get
        {
            return new()
            {
                Name = Name,
                StartedAt = StartedAt,
                Unlocked = Unlocked,
                OutputReady = OutputReady,
                ProductionOptionsAllowed = ProductionOptionsAllowed,
                State = State,
                ReducedBy = ReducedBy,
                Selected = _selected,
                IsSuppressed = IsSuppressed,
                ExtraRewards = ExtraRewards,
                ExtrasResolved = _extrasResolved,
                OutputPromise = OutputPromise,
                AdvancedSpeedBonus = AdvancedSpeedBonus,
                Level = Level,
                MaxBenefits = MaxBenefits,
                IsRental = IsRental,
                // Save the promise only when a run exists; otherwise null
                RunMultiplier = _selected is null ? null : _runMultiplier
            };
        }
    }
    public string RequiredName(int selected) => recipe.Options[selected].Required;
    public string ReceivedName
    {
        get
        {
            if (_selected is null)
            {
                return recipe.Options.First().Output.Item; //just give them the first item this time.
                //throw new CustomBasicException("There was nothing selected");
            }
            return recipe.Options[_selected.Value].Output.Item;
        }
    }
    public int RequiredCount(int selected) => recipe.Options[selected].Input;
    public int Returned(int selected) => recipe.Options[selected].Output.Amount;
    public int AmountInProgress
    {
        get
        {
            if (_selected is null)
            {
                return 0; //try this.
                //throw new CustomBasicException("There was nothing selected");
            }
            if (OutputPromise is null)
            {
                return recipe.Options[_selected.Value].Output.Amount;
            }
            if (OutputPromise.IsDouble)
            {
                return recipe.Options[_selected.Value].Output.Amount * 2;
            }
            return recipe.Options[_selected.Value].Output.Amount;
        }
    }
    public TimeSpan GetDuration(int selected, TimeSpan reducedBy)
    {
        var option = recipe.Options[selected];
        var duration = option.Duration - reducedBy;
        // If producing, use locked promise. If idle (UI preview), use current.
        var m = _runMultiplier ?? _currentMultiplier;
        bool canInstant = recipe.IsFast && MaxBenefits == true;

        double bonusMult = AdvancedSpeedBonus.SpeedBonusToTimeMultiplier(canInstant);
        return duration.Apply(m * bonusMult, canInstant);
    }
    public void Produce(int selected, TimeSpan reducedBy)
    {
        if (State != EnumAnimalState.None)
        {
            return;
        }
        _selected = selected;
        // Lock promise for this run
        _runMultiplier = _currentMultiplier;
        ReducedBy = reducedBy;
        State = EnumAnimalState.Producing;
        OutputReady = Returned(selected);
        Duration = GetDuration(selected, reducedBy);
        StartedAt = DateTime.Now;
        OutputPromise = null;
        ExtraRewards.Clear();
        _extrasResolved = false;
        RunPossibleAugmentation();
        _needsSaving = true;
    }
    private bool _needsSaving;
    private bool _extrasResolved;
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
        if (State == EnumAnimalState.Collecting && OutputPromise is null)
        {
            //check for output augmentation promise
            RunPossibleAugmentation();
        }
        // If we're Collecting and we have a promise but haven't resolved extras yet, do it now.
        if (State == EnumAnimalState.Collecting && OutputPromise is not null && _extrasResolved == false)
        {
            ResolveExtraRewards();
            _extrasResolved = true;
            _needsSaving = true;
        }
        if (State != EnumAnimalState.Producing || StartedAt is null || Duration is null)
        {
            return;
        }
        RunPossibleAugmentation();
        var elapsed = DateTime.Now - StartedAt.Value;
        if (elapsed >= Duration)
        {
            State = EnumAnimalState.Collecting;
            StartedAt = null;
            // If promise exists now, resolve immediately; otherwise it can resolve later when promise appears.
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

        // Guaranteed double-type bonus (no RNG)
        if (OutputPromise.IsDouble)
        {
            int received = OutputReady * 2;

            if (OutputPromise.ExtraRewards.Count == 1 && OutputPromise.ExtraRewards.Single() == ReceivedName)
            {
                OutputReady = received; //computer discouraged doing this way.  i decided to risk doing it my way this time.
                return;
            }

            foreach (var item in OutputPromise.ExtraRewards)
            {
                ExtraRewards.Add(new()
                {
                    Amount = received,
                    Item = item
                });
            }
            return;
        }

        if (_selected is null)
        {
            throw new CustomBasicException("No selection");
        }

        //double totalChanceRaw = OutputPromise.Chance * (_selected.Value + 1);
        int multiplier = _selected.Value + 1;
        double chancePercent = OutputPromise.Chance * multiplier; // 0..100 (can be decimal)
        chancePercent = Math.Clamp(chancePercent, 0d, 100d);


        bool hit = rs1.RollHit(chancePercent);


        // miss
        if (hit == false)
        {
            return;
        }

        // hit => award once per item
        foreach (var item in OutputPromise.ExtraRewards)
        {
            ExtraRewards.Add(new()
            {
                Amount = 1,
                Item = item
            });
        }
    }
    public void Collect()
    {
        if (OutputReady <= 0)
        {
            return;
        }
        OutputReady--;
        if (OutputReady == 0)
        {
            Reset();
            
        }
    }
    public void Reset()
    {
        State = EnumAnimalState.None;


        _selected = null;

        // Clear promise for next run
        _runMultiplier = null;
        _needsSaving = true;
        Duration = null;
        Clear();
    }
    public void Clear()
    {
        OutputPromise = null;
        ExtraRewards.Clear();
        _needsSaving = true;
    }
    public TimeSpan? ReadyTime
    {
        get
        {
            if (State != EnumAnimalState.Producing || StartedAt is null || Duration is null)
            {
                return null;
            }
            var elapsed = DateTime.Now - StartedAt.Value;
            var remaining = Duration - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
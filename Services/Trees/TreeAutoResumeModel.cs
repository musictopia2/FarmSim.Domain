namespace FarmSim.Domain.Services.Trees;
public class TreeAutoResumeModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TreeName { get; set; } = "";
    public bool Unlocked { get; set; } = true;
    public int TreesReady { get; set; }
    public EnumTreeState State { get; set; } = EnumTreeState.Collecting;
    public DateTime? StartedAt { get; set; }
    public DateTime? TempStart { get; set; }
    public double? RunMultiplier { get; set; }
    public TimeSpan ReducedBy { get; set; } = TimeSpan.Zero;
    public bool IsRental { get; set; } //this means if it comes from rental, needs to mark so can lock the exact proper one.
    public bool RentalExpired { get; set; }

    // Temporary override: something else is taking over the rule, so this tree shouldn't appear / be usable
    public bool IsSuppressed { get; set; } = false;

    public OutputAugmentationSnapshot? OutputPromise { get; set; }

    public int Level { get; set; } = 1;
    public double? AdvancedSpeedBonus { get; set; }
    public bool MaxBenefits { get; set; }

    //good news is did not save time here so at least no problem here.

    // Production timing is determined by the associated TreeRecipe.
}
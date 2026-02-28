namespace FarmSim.Domain.Services.Animals;
public class AnimalAutoResumeModel
{
    public Guid Id { get;  set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public EnumAnimalState State { get; set; } = EnumAnimalState.None;
    public bool Unlocked { get; set; } = true;
    public bool IsSuppressed { get; set; } = false;
    public int ProductionOptionsAllowed { get; set; } = 1;
    public int OutputReady { get; set; }
    public double? RunMultiplier { get; set; }
    public TimeSpan ReducedBy { get; set; } = TimeSpan.Zero;
    public bool IsRental { get; set; } //this means if it comes from rental, needs to mark so can lock the exact proper one.
    public DateTime? StartedAt { get; set; }
    public int? Selected { get; set; }
    public bool ExtrasResolved { get; set; }
    public OutputAugmentationSnapshot? OutputPromise { get; set; }
    public BasicList<ItemAmount> ExtraRewards { get; set; } = []; //when you are about to collect, show then.
    public int Level { get; set; } = 1;
    public double? AdvancedSpeedBonus { get; set; }
    public bool MaxBenefits { get; set; }
}
namespace FarmSim.Domain.Services.Workshops;
public class WorkshopAutoResumeModel : ISharedGuid
{
    public Guid Id { get; set; } = Guid.NewGuid(); //since i am sharing now.
    public int SelectedRecipeIndex { get; set; } = 0;
    public string Name { get; set; } = "";
    public BasicList<UnlockModel> SupportedItems { get; set; } = [];
    public bool Unlocked { get; set; } //needs this too now.
    public int Capacity { get; set; } = 2;
    public BasicList<CraftingAutoResumeModel> Queue { get; set; } = [];
    public TimeSpan ReducedBy { get; set; } = TimeSpan.Zero;
    public bool IsRental { get; set; } //this means if it comes from rental, needs to mark so can lock the exact proper one.

    public int Level { get; set; } = 1; //starts at 1.  needs to do lookups.
    public double? AdvancedSpeedBonus { get; set; }
    public bool MaxBenefits { get; set; }
    public double? MaxDropRate { get; set; }
}
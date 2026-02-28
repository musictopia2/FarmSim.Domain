namespace FarmSim.Domain.Services.Workshops;
public class CraftingAutoResumeModel
{
    public string RecipeItem { get; set; } = ""; //needed so it can do a recipe lookup.
    public EnumWorkshopState State { get; set; } = EnumWorkshopState.Waiting;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? RunMultiplier { get; set; }
    public OutputAugmentationSnapshot? OutputPromise { get; set; } //may be here (?)
}
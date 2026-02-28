namespace FarmSim.Domain.Services.Crops;
public class CropAutoResumeModel
{
    public string? Crop { get; set; } // Crop planted (if any)
    public EnumCropState State { get; set; }       // Empty / Growing / Ready
    public DateTime? PlantedAt { get; set; } //when the crop was planted
    public bool Unlocked { get; set; } = true;             // Is this slot unlocked
    public double? RunMultiplier { get; set; } //same idea as with the animals.
    public TimeSpan ReducedBy { get; set; } = TimeSpan.Zero;
    public bool ExtrasResolved { get; set; }
    public OutputAugmentationSnapshot? OutputPromise { get; set; }
    public BasicList<ItemAmount> ExtraRewards { get; set; } = []; //when you are about to collect, show then.
    public double? AdvancedSpeedBonus { get; set; }
    public bool? MaxedBenefits { get; set; }
}
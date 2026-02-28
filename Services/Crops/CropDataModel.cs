namespace FarmSim.Domain.Services.Crops;
public class CropDataModel
{
    public string Item { get; set; } = "";
    public bool Unlocked { get; set; }
    public bool IsSuppressed { get; set; }
    public int Level { get; set; } = 1;
    public double? AdvancedSpeedBonus { get; set; }
    public bool MaxBenefits { get; set; }
}
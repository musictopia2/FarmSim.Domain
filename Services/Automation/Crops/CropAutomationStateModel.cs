namespace FarmSim.Domain.Services.Automation.Crops;
public class CropAutomationStateModel
{
    public required string Name { get; set; }
    public bool Unlocked { get; set; } = true;
    public bool IsSuppressed { get; set; }
    public int VirtualCount { get; set; }
    public int RequestedTotal { get; set; }
    public int StoredUnits { get; set; }
    public int DeliveredTowardRequest { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    //public OutputAugmentationSnapshot? OutputPromise { get; set; }
    public int StoredExtraUnits { get; set; }
    public string? StoredExtraReward { get; set; }
}
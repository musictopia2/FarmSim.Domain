namespace FarmSim.Domain.Services.Automation.Animals;
public class AnimalAutomationStateModel
{
    //for this one, will always base on the fastest option (but double).
    public required string AnimalName { get; set; }
    public bool Unlocked { get; set; } = true;
    public bool IsSuppressed { get; set; }
    public int VirtualCount { get; set; }
    public int RequestedTotal { get; set; }
    public int StoredUnits { get; set; }
    public int DeliveredTowardRequest { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    public DateTime? NextReadyAt { get; set; }
    public int StoredExtraUnits { get; set; }
    public string? StoredExtraReward { get; set; }
}

namespace FarmSim.Domain.Services.Automation.Worksites;
public class WorksiteAutomationStateModel
{
    public required string Location { get; init; }
    public bool Unlocked { get; set; }
    public int RequestedTotal { get; set; }
    public int StoredRequests { get; set; }
    public int DeliveredTowardRequest { get; set; }
    public Dictionary<string, int> FailureHistory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public BasicList<WorkerRecipe> Workers { get; set; } = [];
    public BasicList<WorkerRecipe> PendingWorkers { get; set; } = [];
    public BasicList<ItemAmount> Rewards { get; set; } = [];
    //augmentation comes later.
    public double? RunMultiplier { get; set; }
    public DateTime? BlockedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public OutputAugmentationSnapshot? OutputPromise { get; set; }

}
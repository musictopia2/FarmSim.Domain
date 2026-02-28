namespace FarmSim.Domain.Services.Automation.Workshops;
public class WorkshopAutomationBuildingLaneStateModel
{
    public required string BuildingName { get; set; }  // "Windmill"
    public string? ActiveItem { get; set; } // which item this building is currently crafting
    public DateTime? StartedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    public DateTime? CycleEndsAt { get; set; }
    public bool Unlocked { get; set; } = true;
    public int VirtualCount { get; set; } //this is what count i have for this.
    public BasicList<WorkshopAutomationItemStateModel> Items { get; set; } = [];
}
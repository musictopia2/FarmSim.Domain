namespace FarmSim.Domain.Services.Automation.Workshops;
public class WorkshopAutomationItemStateModel
{
    public required string Item { get; set; }          // "Flour"
    public required string BuildingName { get; set; }  // "Windmill"
    public int RequestedTotal { get; set; }
    public int DeliveredTowardRequest { get; set; }
    public int StoredUnits { get; set; }
    public bool Unlocked { get; set; } //i think defaults to false this time.
}
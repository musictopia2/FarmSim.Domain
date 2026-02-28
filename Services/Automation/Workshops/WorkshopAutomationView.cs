namespace FarmSim.Domain.Services.Automation.Workshops;
public class WorkshopAutomationView
{
    public string BuildingName { get; set; } = "";
    public string? SelectedItem { get; set; }
    //public string ItemRequired { get; set; } = "";
    //public int RequiredAmount { get; set; } //not sure if ui needs it (it may)
    //public DateTime? BlockedAt { get; set; }
    public DateTime? StartedAt { get; set; }
}

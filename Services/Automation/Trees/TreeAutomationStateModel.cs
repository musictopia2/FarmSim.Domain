namespace FarmSim.Domain.Services.Automation.Trees;
public class TreeAutomationStateModel
{
    required public string TreeName { get; set; }
    public int VirtualCount { get; set; }
    public bool Unlocked { get; set; } = true;
    public bool IsSuppressed { get; set; } //if i ever eventually have the instant unlimited, then can't do this anymore because something else took its place.
    //or if i use unlimited speed seeds, would be suppressed temporarily (well see).
    public int RequestedTotal { get; set; }
    public int StoredUnits { get; set; }
    public int DeliveredTowardRequest { get; set; }
    //public DateTime? StartedAt { get; set; }
    //try their way first.
    public DateTime? StartedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    public int StoredExtraUnits { get; set; }
}
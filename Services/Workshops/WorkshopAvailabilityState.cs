namespace FarmSim.Domain.Services.Workshops;
public class WorkshopAvailabilityState 
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Unlocked { get; set; } = true;
    public bool RemainingCraftingJobs { get; set; } //cannot lock a building if in use.
}
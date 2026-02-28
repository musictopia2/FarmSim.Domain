namespace FarmSim.Domain.Services.Workshops;
public class CraftingSummary
{
    public Guid Id { get; set; } //has to transfer from the job itself.
    public string Name { get; set; } = ""; //this is what is being crafted.
    public EnumWorkshopState State { get; set; }
    public string ReadyTime { get; set; } = "";
}
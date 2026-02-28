namespace FarmSim.Domain.Services.Automation.Animals;
public class AnimalAutomationView
{
    public string ItemProduced { get; set; } = "";
    public string AnimalName { get; set; } = "";
    //public string ItemRequired { get; set; } = "";
    //public int RequiredAmount { get; set; } //not sure if ui needs it (it may)
    //public DateTime? BlockedAt { get; set; }
    public DateTime? StartedAt { get; set; }
}

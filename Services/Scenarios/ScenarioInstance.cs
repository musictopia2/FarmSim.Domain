namespace FarmSim.Domain.Services.Scenarios;
public class ScenarioInstance
{
    //this was like my old quest system before i had leveling.
    public string ScenarioId { get; set; } = ""; //the new quest system did.  this should too.
    public string Item { get; set; } = "";

    public int Required { get; set; } //once you complete, then removes from inventory.
    public bool Completed { get; set; }
    public bool Tracked { get; set; }
    public int Order { get; set; }
    public bool Seen { get; set; }
}
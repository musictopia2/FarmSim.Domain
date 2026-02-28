namespace FarmSim.Domain.Services.Scenarios;
public class ScenarioProfileModel
{
    //the scenarios has to be populated the same way the new quest engine did.
    public BasicList<ScenarioInstance> Tasks { get; set; } = []; //these are tasks that need to be completed.
    public EnumScenarioStatus Status { get; set; }
    public TimeSpan TimeBetween { get; set; } //needs help coming up with a better name.  here is where i have to specify that after completing, how long before you can do again.
    public DateTime? LastCompleted { get; set; } //this is when you last completed and claimed it.
    public int Rewards { get; set; } //this is the rewards you will receive from this.
}
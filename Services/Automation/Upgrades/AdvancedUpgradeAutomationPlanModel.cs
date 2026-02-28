namespace FarmSim.Domain.Services.Automation.Upgrades;
public class AdvancedUpgradeAutomationPlanModel
{
    public string Target { get; set; } = ""; //i think its okay to just have the target (crafted name, etc).
    public BasicList<AdvancedUpgradeAutomationTrack> Tracks { get; set; } = [];
}
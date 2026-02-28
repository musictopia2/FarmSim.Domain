namespace FarmSim.Domain.Services.Automation.Upgrades;
public class AdvancedUpgradeAutomationOwnedModel
{
    public string Target { get; set; } = "";
    public EnumAdvancedUpgradeAutomationEffect Effect { get; set; }
    public int Level { get; set; }
}
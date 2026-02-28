namespace FarmSim.Domain.Services.Automation.Upgrades;
public class AdvancedUpgradeAutomationDetailModel
{
    public int? MaximumLevels { get; set; } //may be unlimited
    public string Currency { get; set; } = "";
    public int CostAmount { get; set; }
    public int Level { get; set; } //your current level.  if you had none, then you are level 1 (in ui).
    public int? NextBenefit { get; set; }
    public BasicList<int> ReducedTimes { get; set; } = [];
    public EnumAdvancedUpgradeAutomationEffect Effect { get; set; }
}
namespace FarmSim.Domain.Services.Trees;
public class TreeUpgradeTierRule
{
    public string TreeName { get; set; } = "";
    public BasicList<int> TierLevels { get; set; } = [];
}
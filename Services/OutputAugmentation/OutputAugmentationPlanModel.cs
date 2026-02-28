namespace FarmSim.Domain.Services.OutputAugmentation;
public class OutputAugmentationPlanModel
{
    public string Key { get; init; } = "";
    //like whether its a worksite, or item, etc.
    public string TargetName { get; init; } = ""; //can come from the catalog
    //i assume that for each item on the list, would be just one or double rewards.
    public BasicList<string> Rewards { get; init; } = [];
    //i prefer to do here so can do a better job on the descriptions.
    public bool IsDouble { get; init; } = false;

    //defaults to guaranteed.
    public double ChancePercent { get; init; } = 100; //default to 100 percent
}
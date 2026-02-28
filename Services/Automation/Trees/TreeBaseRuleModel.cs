namespace FarmSim.Domain.Services.Automation.Trees;
public class TreeBaseRuleModel
{
    public required string TreeName { get; init; }
    public required int StartingQueueCount { get; init; }
}
namespace FarmSim.Domain.Services.Automation.Worksites;
public class WorksiteBaseRuleModel
{
    public required string Location { get; init; }
    public required int StartingQueueCount { get; init; }
}
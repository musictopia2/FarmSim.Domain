namespace FarmSim.Domain.Services.Automation.Animals;
public class AnimalBaseRuleModel
{
    public required string AnimalName { get; init; }
    public required int StartingQueueCount { get; init; }
}
namespace FarmSim.Domain.Services.Automation.Crops;
public class CropBaseRuleModel
{
    public required string CropName { get; init; }
    public required int StartingQueueCount { get; init; }
}
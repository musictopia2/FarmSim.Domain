namespace FarmSim.Domain.Services.OutputAugmentation;
public class OutputAugmentationServicesContext
{
    public required IOutputAugmentationPlanProvider OutputAugmentationPlanProvider { get; init; }
}
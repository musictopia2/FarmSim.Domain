namespace FarmSim.Domain.Services.OutputAugmentation;
public class OutputAugmentationSnapshot
{
    public bool IsDouble { get; init; }
    public BasicList<string> ExtraRewards { get; init; } = [];
    public double Chance { get; set; } //this means its not a guaranteed.
}
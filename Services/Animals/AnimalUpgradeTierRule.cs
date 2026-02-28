namespace FarmSim.Domain.Services.Animals;
public class AnimalUpgradeTierRule
{
    public string AnimalName { get; set; } = "";
    public BasicList<int> TierLevels { get; set; } = [];
}
namespace FarmSim.Domain.Services.Animals;
public class AnimalServicesContext
{
    required public IAnimalRecipes AnimalRecipes { get; init; }
    required public IAnimalRepository AnimalRepository { get; init; }
    required public IAnimalUpgradeTierRule AnimalUpgradeTierRule { get; init; }
}
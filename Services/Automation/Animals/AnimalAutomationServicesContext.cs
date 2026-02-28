namespace FarmSim.Domain.Services.Automation.Animals;
public class AnimalAutomationServicesContext
{
    required public IAnimalAutomationProfile AnimalAutomationProfile { get; init; }
    required public IAnimalBaseRulesProvider AnimalBaseRulesProvider { get; init; }
    required public IAnimalRecipes AnimalRecipes { get; init; }
}
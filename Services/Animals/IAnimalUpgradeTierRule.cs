namespace FarmSim.Domain.Services.Animals;
public interface IAnimalUpgradeTierRule
{
    Task<BasicList<AnimalUpgradeTierRule>> GetRulesAsync(FarmKey farm);
}
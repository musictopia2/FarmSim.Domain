namespace FarmSim.Domain.Services.Automation.Animals;
public interface IAnimalBaseRulesProvider
{
    Task<BasicList<AnimalBaseRuleModel>> GetRulesAsync(FarmKey farm);
}
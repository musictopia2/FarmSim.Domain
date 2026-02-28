namespace FarmSim.Domain.Services.Trees;
public interface ITreeUpgradeTierRules
{
    Task<BasicList<TreeUpgradeTierRule>> GetRulesAsync(FarmKey farm);
}
namespace FarmSim.Domain.Services.Automation.Trees;
public interface ITreeBaseRulesProvider
{
    Task<BasicList<TreeBaseRuleModel>> GetRulesAsync(FarmKey farm);
}
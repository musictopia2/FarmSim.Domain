namespace FarmSim.Domain.Services.Automation.Worksites;
public interface IWorksiteBaseRulesProvider
{
    Task<BasicList<WorksiteBaseRuleModel>> GetRulesAsync(FarmKey farm);
}
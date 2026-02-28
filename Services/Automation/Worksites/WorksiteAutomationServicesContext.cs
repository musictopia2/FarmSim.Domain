namespace FarmSim.Domain.Services.Automation.Worksites;
public class WorksiteAutomationServicesContext
{
    required public IWorksiteAutomationProfile WorksiteAutomationProfile { get; init; }
    required public IWorksiteBaseRulesProvider WorksiteBaseRulesProvider { get; init; }
    required public IWorksiteRecipes WorksiteRecipes { get; init; }
}
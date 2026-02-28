namespace FarmSim.Domain.Services.Automation.Trees;
public class TreeAutomationServicesContext
{
    required public ITreeAutomationProfile Profile { get; init; }
    required public ITreeBaseRulesProvider TreeBaseRulesProvider { get; init; }
    required public ITreeRecipes TreeRecipes { get; init; }
    required public ITreesCollecting TreesCollecting { get; init; } //still needed for the speed seeds.
}
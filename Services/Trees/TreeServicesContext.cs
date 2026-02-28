namespace FarmSim.Domain.Services.Trees;
public class TreeServicesContext
{
    required public ITreeRecipes TreeRecipes { get; init; }
    required public ITreeRepository TreeRepository { get; init; }
    required public ITreesCollecting TreesCollecting { get; init; }
    required public ITreeUpgradeTierRules UpgradeTierRules { get; init; }
}
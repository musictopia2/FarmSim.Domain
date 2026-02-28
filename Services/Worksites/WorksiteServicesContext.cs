namespace FarmSim.Domain.Services.Worksites;
public class WorksiteServicesContext
{
    required public IWorksiteRecipes WorksiteRecipes { get; init; }
    required public IWorksiteRepository WorksiteRepository { get; init; }
}
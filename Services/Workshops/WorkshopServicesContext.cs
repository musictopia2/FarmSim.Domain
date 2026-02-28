namespace FarmSim.Domain.Services.Workshops;
public class WorkshopServicesContext
{
    required public IWorkshopRecipes WorkshopRecipes { get; init; }
    required public IWorkshopRespository WorkshopRespository { get; init; }
}
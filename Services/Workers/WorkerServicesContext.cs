namespace FarmSim.Domain.Services.Workers;
public class WorkerServicesContext
{
    required public IWorkerRecipes WorkerRecipes { get; init; }
    required public IWorkerRepository WorkerRepository { get; init; }
}
namespace FarmSim.Domain.Services.Workers;
public interface IWorkerRecipes
{
    Task<BasicList<WorkerRecipe>> GetWorkersAsync();
}
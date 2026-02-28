namespace FarmSim.Domain.Services.Scenarios;
public interface IScenarioGenerationService
{
    BasicList<ScenarioInstance> GetScenarios();
}
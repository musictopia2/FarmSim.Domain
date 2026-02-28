namespace FarmSim.Domain.Services.Scenarios;
public class ScenarioServicesContext
{
    //i probably need another service to generate the scenarios.
    public required IScenarioProfile ScenarioProfile { get; init; }
    public required IScenarioGenerationService ScenarioGeneration { get; init; }
    public required IInventoryStarterRepository InventoryStarterRepository { get; init; }
}
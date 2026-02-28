namespace FarmSim.Domain.Services.Automation.Animals;
public interface IAnimalAutomationFactory
{
    AnimalAutomationServicesContext GetAnimalServices(FarmKey farm);
}
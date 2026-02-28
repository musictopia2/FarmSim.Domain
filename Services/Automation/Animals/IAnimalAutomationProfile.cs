namespace FarmSim.Domain.Services.Automation.Animals;
public interface IAnimalAutomationProfile
{
    Task<BasicList<AnimalAutomationStateModel>> LoadAsync();
    Task SaveAsync(BasicList<AnimalAutomationStateModel> animals);
}
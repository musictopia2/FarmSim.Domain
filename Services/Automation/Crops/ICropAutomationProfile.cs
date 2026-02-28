namespace FarmSim.Domain.Services.Automation.Crops;
public interface ICropAutomationProfile
{
    Task<BasicList<CropAutomationStateModel>> LoadAsync();
    Task SaveAsync(BasicList<CropAutomationStateModel> crops);
}
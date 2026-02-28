namespace FarmSim.Domain.Services.Automation.Crops;
public class CropAutomationServicesContext
{
    required public ICropAutomationProfile CropAutomationProfile { get; init; }
    required public ICropBaseRulesProvider CropBaseRulesProvider { get; init; }
    required public ICropRecipes CropRecipes { get; init; }
}
namespace FarmSim.Domain.Services.Automation.Crops;
public interface ICropAutomationFactory
{
    CropAutomationServicesContext GetCropServices(FarmKey farm);
}
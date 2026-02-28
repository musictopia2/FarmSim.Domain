namespace FarmSim.Domain.Services.AdvancedSettings;
public interface IAdvancedSettingsFactory
{
    AdvancedSettingsServicesContext GetAdvancedSettingsServices(FarmKey farm);
}
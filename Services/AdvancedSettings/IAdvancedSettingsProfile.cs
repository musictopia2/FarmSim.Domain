namespace FarmSim.Domain.Services.AdvancedSettings;
public interface IAdvancedSettingsProfile
{
    Task<AdvancedSettingsProfileModel> LoadAsync();
    Task SaveAsync(AdvancedSettingsProfileModel settings);
}
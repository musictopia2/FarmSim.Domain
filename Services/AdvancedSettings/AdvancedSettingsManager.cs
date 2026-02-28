namespace FarmSim.Domain.Services.AdvancedSettings;
public class AdvancedSettingsManager
{
    private AdvancedSettingsProfileModel _settings = null!;
    private IAdvancedSettingsProfile _profileStorage = null!;
    public async Task SetAdvancdSettingsStyleContextAsync(AdvancedSettingsServicesContext context)
    {
        _profileStorage = context.AdvancedSettingsProfile;
        _settings = await _profileStorage.LoadAsync();
    }
    public AdvancedSettingsProfileModel Settings => _settings;
    public async Task SaveSettingsAsync(FarmKey farm, AdvancedSettingsProfileModel settings, GameRegistry registry)
    {
        if (farm.IsMain == false)
        {
            throw new CustomBasicException("Only the main farm can save settings");
        }
        _settings = settings.Clone();
        await _profileStorage.SaveAsync(_settings);

        FarmKey other;
        other = farm.AsCoin;
        MainFarmContainer container;
        container = registry.GetFarm(other);
        await container.AdvancedSettingsManager.SaveOtherSettingsAsync(settings);
        other = farm.AsCooperative;
        container = registry.GetFarm(other);
        await container.AdvancedSettingsManager.SaveOtherSettingsAsync(settings);
    }
    private async Task SaveOtherSettingsAsync(AdvancedSettingsProfileModel settings)
    {
        _settings = settings.Clone(); //this is safer.
        await _profileStorage.SaveAsync(settings);
    }
}
namespace FarmSim.Domain.Services.Automation.Gifts;
public interface IGiftProfile
{
    Task<GiftProfileModel> LoadAsync(FarmKey farm);
    Task SaveAsync(FarmKey farm, GiftProfileModel profile);
}
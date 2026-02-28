namespace FarmSim.Domain.Services.Automation.Gifts;
public interface IGiftPlanProvider
{
    Task<GiftPlanModel> GetPlanAsync(FarmKey farm);
}
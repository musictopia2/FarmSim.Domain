namespace FarmSim.Domain.Services.Automation.Crops;
public interface ICropBaseRulesProvider
{
    Task<BasicList<CropBaseRuleModel>> GetRulesAsync(FarmKey farm);
}
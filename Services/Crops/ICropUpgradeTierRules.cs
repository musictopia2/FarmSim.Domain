namespace FarmSim.Domain.Services.Crops;
public interface ICropUpgradeTierRules
{
    Task<BasicList<CropUpgradeTierRule>> GetRulesAsync(FarmKey farm);
}
namespace FarmSim.Domain.Services.Rules;
public interface IFarmRulesProvider
{
    Task<EnumRuleCategory> GetRulesAsync(string playerName);
    Task SaveRulesAsync(string playerName, EnumRuleCategory category);
}
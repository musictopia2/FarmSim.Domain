namespace FarmSim.Domain.Services.Rules;
public class FarmRuleModel
{
    public string PlayerName { get; set; } = "";
    public EnumRuleCategory CategoryChosen { get; set; } = EnumRuleCategory.NotChosen;
}
namespace FarmSim.Domain.Services.Rules;
public class RulesManager
{
    public bool AutomationEnabled { get; private set; } = false;
    public EnumRuleCategory RuleChosen { get; private set;  }

    public void SetStyleContext(FarmKey farm, EnumRuleCategory category)
    {
        if (category == EnumRuleCategory.NotChosen)
        {
            throw new CustomBasicException("No category was chosen.  Should not even use this yet");
        }
        AutomationEnabled = farm.IsMain && category == EnumRuleCategory.Automated;
        RuleChosen = category;
    }
}
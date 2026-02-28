namespace FarmSim.Domain.Services.Automation.Upgrades;
public class AdvancedUpgradeAutomationTrack
{
    public EnumAdvancedUpgradeAutomationEffect Effect { get; set; }
    public bool IsUnlimited { get; set; }
    public int MaxLevel { get; set; }
    public LevelLadderDto Ladder { get; set; } = new();
    public string CostCurrencyKey { get; set; } = CurrencyKeys.Coin;
}

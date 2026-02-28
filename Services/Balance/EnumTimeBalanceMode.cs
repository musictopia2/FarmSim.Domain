namespace FarmSim.Domain.Services.Balance;
public enum EnumTimeBalanceMode
{
    StandardManual,     // used by Main + Coop
    StandardAutomated,  // used by Main when automation is enabled
    Coin                // used by Coin farms (automation ignored)
}
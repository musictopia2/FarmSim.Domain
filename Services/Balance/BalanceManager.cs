namespace FarmSim.Domain.Services.Balance;
public class BalanceManager(IBaseBalanceProvider baseBalanceProvider,
    RulesManager rulesManager
    )
{
    private BaseBalanceProfile? _base;
    public BaseBalanceProfile Base =>
        _base ?? throw new CustomBasicException("BalanceManager was not initialized. Call SetStyleContextAsync first.");
    public async Task SetStyleContextAsync(FarmKey farm)
    {
        EnumTimeBalanceMode mode;
        if (rulesManager.AutomationEnabled)
        {
            mode = EnumTimeBalanceMode.StandardAutomated;
        }
        else if (farm.IsCoin)
        {
            mode = EnumTimeBalanceMode.Coin;
        }
        else
        {
            mode = EnumTimeBalanceMode.StandardManual;
        }
        _base = await baseBalanceProvider.GetBaseBalanceAsync(farm.ProfileId, mode);
    }
}
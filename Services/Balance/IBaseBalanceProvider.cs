namespace FarmSim.Domain.Services.Balance;
public interface IBaseBalanceProvider
{
    Task<BaseBalanceProfile> GetBaseBalanceAsync(string profileId, EnumTimeBalanceMode mode); //all you care about is the profile id and the mode alone.
}
using System.Collections.Concurrent; //not common enough to be in global usings.
namespace FarmSim.Domain.Services.Automation.Gifts;
public class GiftLedgerService(IGiftFactory giftFactory)
{
    private readonly ConcurrentDictionary<GiftLedgerKey, BasicList<GiftLedgerModel>> _listCache = new();
    private readonly ConcurrentDictionary<GiftLedgerKey, int> _balanceCache = new();
    public async Task DepositAsync(string playerId, string profileId, int amount)
    {
        //this is what an api can call into.
        if (amount <= 0)
        {
            throw new CustomBasicException("The deposit must be greater than 0.");
        }
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new CustomBasicException("PlayerId is required.");
        }
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new CustomBasicException("ProfileId is required.");
        }
        var db = giftFactory.GetGiftServices().GiftLedgerService;
        var key = new GiftLedgerKey(playerId, profileId);
        await db.DepositAsync(playerId, profileId, amount);
        Remove(key);
    }
    public async Task<BasicList<GiftLedgerModel>> GetHistoryAsync(string playerId, string profileId) => await GetStateAsync(playerId, profileId);
    private async Task<BasicList<GiftLedgerModel>> GetStateAsync(string playerId, string profileId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new CustomBasicException("PlayerId is required.");
        }
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new CustomBasicException("ProfileId is required.");
        }
        var key = new GiftLedgerKey(playerId, profileId);

        if (_listCache.TryGetValue(key, out var state))
        {
            _balanceCache.TryAdd(key, state.Sum(x => x.Amount));
            return state;
        }
        state = await giftFactory.GetGiftServices().GiftLedgerService.LoadAsync(playerId, profileId);
        _listCache[key] = state;
        _balanceCache[key] = state.Sum(x => x.Amount);
        return state;
    }
    public async Task<int> UseGiftAsync(FarmKey farm, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        // Ensure caches are populated
        await GetStateAsync(farm.PlayerName, farm.ProfileId);

        var key = new GiftLedgerKey(farm.PlayerName, farm.ProfileId);

        int totalHas = _balanceCache.TryGetValue(key, out var bal) ? bal : 0;
        int requested = Math.Min(amount, totalHas);
        if (requested <= 0)
        {
            return 0;
        }

        await giftFactory.GetGiftServices().GiftLedgerService.UseGiftAsync(farm, requested);
        Remove(key);
        return requested;
    }
    private void Remove(GiftLedgerKey key)
    {
        _listCache.TryRemove(key, out _);
        _balanceCache.TryRemove(key, out _);
    }
    public async Task<int> GetBalanceAsync(FarmKey farm)
    {
        await GetStateAsync(farm.PlayerName, farm.ProfileId);
        var key = new GiftLedgerKey(farm.PlayerName, farm.ProfileId);
        return _balanceCache.TryGetValue(key, out var bal) ? bal : 0;
    }
}
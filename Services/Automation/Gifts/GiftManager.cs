namespace FarmSim.Domain.Services.Automation.Gifts;
public class GiftManager(InventoryManager inventoryManager, RulesManager rulesManager, GiftLedgerService giftLedgerService)
{
    private bool _init;
    private GiftPlanModel _plan = null!;
    private GiftProfileModel _personal = null!;
    private IGiftProfile _profile = null!;
    private FarmKey _farm = default;
    public async Task SetStyleContextAsync(GiftServicesContext context, FarmKey farm)
    {
        if (farm.IsCoin || farm.IsCooperative || rulesManager.AutomationEnabled == false)
        {
            return; //must be automation mode in order to use this.   can't be coop or coin farms.
        }
        _plan = await context.GiftPlanProvider.GetPlanAsync(farm);
        _farm = farm;
        _profile = context.GiftProfile;
        _personal = await context.GiftProfile.LoadAsync(farm);
        _init = true;
    }
    public async Task<BasicList<GiftLedgerModel>> GetHistoryAsync() => await giftLedgerService.GetHistoryAsync(_farm.PlayerName, _farm.ProfileId);
    public async Task UpdateTickAsync()
    {
        if (_init == false)
        {
            return;
        }
        if (_plan.Amount <= 0)
        {
            return;
        }
        DateTime? lastDate = _personal.ReceivedLastAt;
        if (lastDate.HasValue)
        {
            DateTime nextAdd = lastDate.Value.Add(_plan.Frequency);
            DateTime current = DateTime.Now;
            if (current < nextAdd)
            {
                return;
            }
        }
        int requested = _plan.Amount;
        int toAdd = await giftLedgerService.UseGiftAsync(_farm, requested);
        if (toAdd == 0)
        {
            return; //nothing to add.  means do nothing.
        }
        _personal.ReceivedLastAt = DateTime.Now;
        _personal.AmountReceived = toAdd;
        await _profile.SaveAsync(_farm, _personal);
        inventoryManager.DirectDeposit(toAdd);
    }
}
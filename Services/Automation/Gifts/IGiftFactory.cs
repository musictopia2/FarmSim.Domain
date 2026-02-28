namespace FarmSim.Domain.Services.Automation.Gifts;
public interface IGiftFactory
{
    public GiftServicesContext GetGiftServices(); //can't ask for farm because there is no farm given here.
}
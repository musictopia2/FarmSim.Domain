namespace FarmSim.Domain.Services.Automation.Gifts;
public class GiftServicesContext
{
    public required IGiftLedgerService GiftLedgerService { get; set; }
    public required IGiftProfile GiftProfile { get; set; }
    public required IGiftPlanProvider GiftPlanProvider { get; set; }
}
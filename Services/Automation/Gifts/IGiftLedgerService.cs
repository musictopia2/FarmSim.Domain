namespace FarmSim.Domain.Services.Automation.Gifts;
public interface IGiftLedgerService
{
    Task<BasicList<GiftLedgerModel>> LoadAsync(string playerId, string profileId);
    Task DepositAsync(string playerId, string profileId, int amountDeposited); //whoever saves it is responsible for depositing into this service.
    Task UseGiftAsync(FarmKey farm, int amountUsed);
}
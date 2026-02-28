namespace FarmSim.Domain.Services.Automation.Gifts;
public record GiftDepositRequest(string PlayerId, string ProfileId, int Amount);
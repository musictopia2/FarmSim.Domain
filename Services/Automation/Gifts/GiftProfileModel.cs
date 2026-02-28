namespace FarmSim.Domain.Services.Automation.Gifts;
public class GiftProfileModel
{
    public DateTime? ReceivedLastAt { get; set; }
    public int AmountReceived { get; set; } //if you received not as much, then you can later get more (if more is deposited).
}
namespace FarmSim.Domain.Services.Automation.Gifts;
public class GiftLedgerModel
{
    public DateTime DateOfEvent { get; set; }
    public int Amount { get; set; } //positive means received, negative means given
    public string Theme { get; set; } = ""; //if the amount was taken, then shows which theme took it.
}
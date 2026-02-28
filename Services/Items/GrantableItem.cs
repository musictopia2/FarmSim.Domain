namespace FarmSim.Domain.Services.Items;
public class GrantableItem
{
    required public string Item { get; init; } = "";
    required public string Source { get; init; } = ""; //needed since you may need this lookup.
    required public int Amount { get; init; }
    required public EnumItemCategory Category { get; init; }
}
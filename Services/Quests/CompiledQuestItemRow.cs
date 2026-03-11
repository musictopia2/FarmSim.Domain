namespace FarmSim.Domain.Services.Quests;
//attempt to do this way.  would mean if a person chose to implement quest generator using this, they can.
public class CompiledQuestItemRow
{
    public required string ItemName { get; init; } = "";
    public required EnumItemCategory ItemCategory { get; init; }
    public int PlayerLevel { get; init; }
    //try to not worry about theme
    public int ItemWeight { get; init; }
    public required int QuantityMin { get; init; }
    public required int QuantityMax { get; init; }
}
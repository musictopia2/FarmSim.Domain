namespace FarmSim.Domain.Services.Quests;
//attempt to do this way.  would mean if a person chose to implement quest generator using this, they can.
public class CompiledQuestItemRow
{
    public required string ItemName { get; init; } = "";
    public required EnumItemCategory ItemCategory { get; init; }
    public required int PlayerLevel { get; init; }
    //try to not worry about theme
    public required int ItemWeight { get; init; }
    public required BasicList<int> Ranges { get; init; } = [];
}
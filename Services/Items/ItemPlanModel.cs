namespace FarmSim.Domain.Services.Items;
//this does not do any random stuff now.
public sealed record ItemPlanModel
{
    public int MinLevel { get; init; }
    public string ItemName { get; init; } = "";
    public EnumItemCategory Category { get; init; }
    public string Source { get; init; } = "";
}
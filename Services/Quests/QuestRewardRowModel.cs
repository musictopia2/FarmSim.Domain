namespace FarmSim.Domain.Services.Quests;
public class QuestRewardRowModel
{
    public required int MinLevel { get; set; }
    public int? MaxLevel { get; set; }
    public required Dictionary<string, int> Rewards { get; set; }
}
namespace FarmSim.Domain.Services.Quests;
public class QuestRewardRow
{
    public required int MinLevel { get; set; }
    public int? MaxLevel { get; set; }
    public required Dictionary<string, int> Rewards { get; set; }
}
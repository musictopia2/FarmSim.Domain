namespace FarmSim.Domain.Services.Quests;
public class QuestInstanceModel
{

    //this will not focus on forever mode.  that is future enhancement (way later).

    public EnumQuestStatus Status { get; set; }
    public string QuestId { get; set; } = "";      // GUID string
    public int LevelRequired { get; set; } //i think this is needed.
    public string ItemName { get; set; } = "";
    public int Required { get; set; }

    public bool Seen { get; set; }                  // first time it appeared on board
    public bool Tracked { get; set; }               // pinned (cap 4)
    public int Order { get; set; }


    public Dictionary<string, int> Rewards { get; set; } = [];

}

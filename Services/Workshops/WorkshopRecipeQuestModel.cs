namespace FarmSim.Domain.Services.Workshops;
public class WorkshopRecipeQuestModel
{
    public string BuildingName { get; init; } = "";
    public string Item { get; init; } = "";
    public Dictionary<string, int> Inputs { get; init; } = [];
    public TimeSpan Duration { get; init; }
}
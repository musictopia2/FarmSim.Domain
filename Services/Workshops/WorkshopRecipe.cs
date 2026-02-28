namespace FarmSim.Domain.Services.Workshops;
public class WorkshopRecipe
{
    public string BuildingName { get; init; } = "";
    public string Item { get; init; } = "";
    public Dictionary<string, int> Inputs { get; init; } = [];
    public ItemAmount Output { get; init; }
    public TimeSpan Duration { get; init; }

    public static void AddWorkshopRecipe(
        BasicList<WorkshopRecipe> list,
        string item,
        string buildingName,
        TimeSpan duration,
        Action<Dictionary<string, int>> inputs,
        int outputAmount = 1)
    {
        var dict = new Dictionary<string, int>();
        inputs(dict);

        list.Add(new WorkshopRecipe
        {
            Item = item,
            BuildingName = buildingName,
            Inputs = dict,
            Output = new ItemAmount(item, outputAmount), // ✅ always matches item
            Duration = duration,
        });
    }

}
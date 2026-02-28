namespace FarmSim.Domain.Services.Animals;
public class AnimalRecipe
{
    public string Animal { get; init; } = "";
    public BasicList<AnimalProductionOption> Options { get; init; } = [];
    required public bool IsFast { get; init; }
}
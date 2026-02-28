namespace FarmSim.Domain.Services.Worksites;
public class WorksiteRecipe
{
    public Dictionary<string, int> Inputs { get; init; } = [];
    public string Location { get; set; } = ""; //try to put location here.
    public TimeSpan Duration { get; init; }
    public BasicList<WorksiteBaselineBenefit> BaselineBenefits { get; init; } = [];
    public int MaximumWorkers { get; set; }
    required
    public string StartText { get; init; }
    public bool CanFocus { get; set; } = true; //default to true.  so some worksites can't focus.
}
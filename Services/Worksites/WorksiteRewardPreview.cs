namespace FarmSim.Domain.Services.Worksites;
public class WorksiteRewardPreview
{
    public string Item { get; set; } = "";
    public int Amount { get; set; }
    public double Chance { get; set; }
    public bool Optional { get; set; } //optional does not count either.
}
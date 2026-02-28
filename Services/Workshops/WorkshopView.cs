namespace FarmSim.Domain.Services.Workshops;
public class WorkshopView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int SelectedRecipeIndex { get; set; }
    public int ReadyCount { get; set; }
    public bool IsRental { get; set; }
    public bool Unlocked { get; set; }
}

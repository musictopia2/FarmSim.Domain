namespace FarmSim.Domain.Services.Trees;
public class TreeView
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = "";
    public string TreeName { get; set; } = "";
    public bool IsRental { get; set; } //now needs to know if its a rental.
    public bool IsFast { get; set; }
}
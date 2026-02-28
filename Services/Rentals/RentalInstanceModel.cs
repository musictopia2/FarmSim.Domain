namespace FarmSim.Domain.Services.Rentals;
public class RentalInstanceModel
{
    public string TargetName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public EnumCatalogCategory Category { get; set; } //this is needed so this can use the proper managers.
    public EnumRentalState State { get; set; } = EnumRentalState.Active;
    public Guid? TargetInstanceId { get; set; }
}
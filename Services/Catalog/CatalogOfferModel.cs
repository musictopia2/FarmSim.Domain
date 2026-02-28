namespace FarmSim.Domain.Services.Catalog;
public class CatalogOfferModel
{
    public EnumCatalogCategory Category { get; init; }
    public string TargetName { get; init; } = "";
    public int LevelRequired { get; set; }
    public Dictionary<string, int> Costs { get; set; } = [];
    public bool Repeatable { get; set; } //so the store can reflect this.
    public TimeSpan? Duration { get; set; } //null means this will not expire.
    public TimeSpan? ReduceBy { get; init; } //the new power pin feature needs it.   others need it to decide if you are going to purchase or not.
    public int Quantity { get; init; } = 1;
    public string? OutputAugmentationKey { get; init; }
}
namespace FarmSim.Domain.Services.Store;
public class StoreItemRowModel
{
    //since my cloning is not working with init, for now, has to do this way (with sets).
    public EnumCatalogCategory Category { get; init; }
    public string TargetName { get; init; } = "";

    // 1) LevelRequired (for the NEXT purchase tier)
    public int LevelRequired { get; init; }

    // 2) Total possible items (how many tiers exist)
    public int TotalPossible { get; init; }

    // 3) Currently owned items
    public int OwnedCount { get; init; }

    // 4) Costs for the NEXT purchase tier (multi-currency)
    public Dictionary<string, int> Costs { get; init; } = [];

    // These make UI display easy (recommended)
    public bool IsLocked { get; init; }      // locked by level
    public bool IsMaxedOut { get; init; }    // owned >= total possible
    public bool IsFree => Costs.Count == 0;


    public bool Repeatable { get; init; }          // from CatalogOfferModel
    public TimeSpan? Duration { get; init; }       // from CatalogOfferModel
    public TimeSpan? ReducedBy { get; init; }
    public int Quantity { get; init; }
    public string? OutputAugmentationKey { get; init; }

    //has to do rental state later.
    public EnumRentalState? RentalState { get; init; }
    public string? RentalDetails { get; init; } //this is what the ui would display.
}
namespace FarmSim.Domain.Services.Catalog;
public static class CatalogExtensions
{
    extension (CatalogOfferModel source)
    {
        public CatalogOfferModel DeepCopy()
        {
            CustomBasicException.ThrowIfNull(source);

            return new CatalogOfferModel
            {
                Category = source.Category,
                TargetName = source.TargetName,
                LevelRequired = source.LevelRequired,
                Duration = source.Duration,
                Repeatable = source.Repeatable,
                ReduceBy = source.ReduceBy,
                Quantity = source.Quantity,
                OutputAugmentationKey = source.OutputAugmentationKey,
                // Deep-copy the costs dictionary
                Costs = source.Costs is null
                    ? []
                    : source.Costs.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value
                    )
            };
        }
    }
}
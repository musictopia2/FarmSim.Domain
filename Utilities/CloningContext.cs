namespace FarmSim.Domain.Utilities;
internal class CloningContext : MappingCloningContext
{
    protected override void Configure(ICustomConfig config)
    {
        config.Make<AdvancedSettingsProfileModel>(c => c.Cloneable(false))
            .Make<InventoryStorageProfileModel>(c => c.Cloneable(false))
            .Make<ProgressionProfileModel>(c => c.Cloneable(false))
            .Make<GiftLedgerModel>(c => c.Cloneable(false))
            .Make<StoreItemRowModel>(c => c.Cloneable(false, a => a.Ignore(p => p.Costs)));
            ;
    }
}
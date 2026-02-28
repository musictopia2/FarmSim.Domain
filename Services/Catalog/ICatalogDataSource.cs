namespace FarmSim.Domain.Services.Catalog;
public interface ICatalogDataSource
{
    Task<BasicList<CatalogOfferModel>> GetCatalogAsync(FarmKey farm);
    Task<BasicList<CatalogOfferModel>> GetCatalogAsync(FarmKey farm, EnumCatalogCategory category); //now you need both.
}
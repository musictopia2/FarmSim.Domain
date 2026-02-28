namespace FarmSim.Domain.Services.Catalog;
public class CatalogManager
{
    private BasicList<CatalogOfferModel> _offers = [];
    public async Task SetCatalogStyleContextAsync(CatalogServicesContext context,
        FarmKey farm)
    {
        _offers = await context.CatalogDataSource.GetCatalogAsync(farm);
        //try to not force there to be a catalog (since i have the alternative farms now).
        //if (_offers.Count == 0)
        //{
        //    throw new CustomBasicException("No Offers");
        //}
    }
    public BasicList<CatalogOfferModel> GetAllOffers(EnumCatalogCategory category) =>
    _offers.Where(x => x.Category == category)
           
           .Select(x => x.DeepCopy())
           .ToBasicList();


    public BasicList<CatalogOfferModel> GetFreeOffers(EnumCatalogCategory category) =>
        _offers.Where(x => x.Category == category && x.Costs.Count == 0)
               .OrderBy(x => x.TargetName)
               .ThenBy(x => x.LevelRequired)
               .Select(x => x.DeepCopy())
               .ToBasicList();

    

}
namespace FarmSim.Domain.Services.Quests;
public interface ICategoryWeightProvider
{
    Task<BasicList<CategoryWeightRowModel>> GetCategoriesAsync(FarmKey farm);
}
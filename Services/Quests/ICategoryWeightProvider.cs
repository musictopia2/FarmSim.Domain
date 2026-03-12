namespace FarmSim.Domain.Services.Quests;
public interface ICategoryWeightProvider
{
    Task<BasicList<CategoryWeightRow>> GetCategoriesAsync(FarmKey farm);
}
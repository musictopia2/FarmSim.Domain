namespace FarmSim.Domain.Services.Crops;
public interface ICropRecipes
{
    Task<BasicList<CropRecipe>> GetCropsAsync();
}
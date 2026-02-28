namespace FarmSim.Domain.Services.Workshops;
public interface IWorkshopRecipes
{
    Task<BasicList<WorkshopRecipe>> GetWorkshopRecipesAsync();
}
namespace FarmSim.Domain.Services.Worksites;
public interface IWorksiteRecipes
{
    Task<BasicList<WorksiteRecipe>> GetWorksitesAsync();
}
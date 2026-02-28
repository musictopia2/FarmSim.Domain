namespace FarmSim.Domain.Services.Animals;
public interface IAnimalRecipes
{
    Task<BasicList<AnimalRecipe>> GetAnimalsAsync();
}
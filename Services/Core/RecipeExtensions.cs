namespace FarmSim.Domain.Services.Core;
public static class RecipeExtensions
{
    public static Func<Task<BasicList<AnimalRecipe>>>? ExtraAnimalRecipes { get; set; }
    public static Func<Task<BasicList<CropRecipe>>>? ExtraCropRecipes { get; set; }
    public static Func<Task<BasicList<TreeRecipe>>>? ExtraTreeRecipes { get; set; }
    public static Func<Task<BasicList<WorkshopRecipe>>>? ExtraWorkshopRecipes { get; set; }
    public static Func<Task<BasicList<WorksiteRecipe>>>? ExtraWorksiteRecipes { get; set; }
    public static async Task<BasicList<T>> MergeAsync<T>(
        BasicList<T> baseRecipes,
        Func<Task<BasicList<T>>>? extraProvider)
    {
        if (extraProvider is null)
        {
            return baseRecipes;
        }

        var extra = await extraProvider();
        if (extra is null || extra.Count == 0)
        {
            return baseRecipes;
        }

        baseRecipes.AddRange(extra);
        return baseRecipes;
    }
}
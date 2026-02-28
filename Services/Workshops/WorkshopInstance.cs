namespace FarmSim.Domain.Services.Workshops;
public class WorkshopInstance
{
    public Guid Id { get; private set; }
    public int SelectedRecipeIndex { get; set; } = 0;
    public BasicList<UnlockModel> SupportedItems { get; set; } = [];
    public int Capacity { get; set; } = 2; //for now, always 2.  later will rethink.
    public bool Unlocked { get; set; }
    public int Level { get; set; } = 1; //starts at 1.  needs to do lookups.
    public double? AdvancedSpeedBonus { get; set; }
    public bool MaxBenefits { get; set; }
    public double? MaxDropRate { get; set; }
    public bool IsRental { get; set; } //this means if it comes from rental, needs to mark so can lock the exact proper one.
    public TimeSpan ReducedBy { get; set; } = TimeSpan.Zero;
    public BasicList<CraftingJobInstance> Queue { get; } = [];
    required public string BuildingName { get; init; }
    public bool CanAccept(WorkshopRecipe recipe)
    {
        if (recipe.BuildingName != BuildingName)
        {
            return false;
        }
        if (Queue.Count >= Capacity)
        {
            return false;
        }
        return true;
    }
    
    public void Load(WorkshopAutoResumeModel workshop, BasicList<WorkshopRecipe> recipes, double multiplier,
        TimedBoostManager timedBoostManager, OutputAugmentationManager outputAugmentationManager
        )
    {
        Capacity = workshop.Capacity;
        SupportedItems = workshop.SupportedItems;
        if (SupportedItems.Count == 0)
        {
            throw new CustomBasicException("Must support at least one item.");
        }
        Id = workshop.Id;
        IsRental = workshop.IsRental;
        Unlocked = workshop.Unlocked;
        SelectedRecipeIndex = workshop.SelectedRecipeIndex;
        ReducedBy = workshop.ReducedBy;
        Level = workshop.Level;
        AdvancedSpeedBonus = workshop.AdvancedSpeedBonus;
        MaxBenefits = workshop.MaxBenefits;
        MaxDropRate = workshop.MaxDropRate;
        Queue.Clear();
        foreach (var item in workshop.Queue)
        {
            WorkshopRecipe recipe = recipes.Single(x => x.Item == item.RecipeItem);
            CraftingJobInstance job = new(recipe, multiplier, workshop.ReducedBy, workshop.AdvancedSpeedBonus, timedBoostManager, outputAugmentationManager);
            job.Load(item);
            Queue.Add(job);
        }
    }
    public WorkshopAutoResumeModel GetWorkshopForSaving
    {
        get
        {
            return new()
            {
                Capacity = Capacity,
                Name = BuildingName,
                Unlocked = Unlocked,
                SupportedItems = SupportedItems,
                ReducedBy = ReducedBy,
                IsRental = IsRental,
                AdvancedSpeedBonus = AdvancedSpeedBonus,
                MaxBenefits = MaxBenefits,
                MaxDropRate = MaxDropRate,
                Level = Level,
                Id = Id, //somehow even if something did not change, its needed.
                Queue = Queue.Select(x => x.GetCraftingForSaving).ToBasicList(),
                SelectedRecipeIndex = SelectedRecipeIndex
            };
        }
    }
}
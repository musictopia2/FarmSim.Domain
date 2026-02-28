namespace FarmSim.Domain.Services.AdvancedSettings;
public class AdvancedSettingsProfileModel
{
    public bool UseConfirmations { get; set; } = true; //default to true.
    public bool UseBoostImmediatelyUponPurchase { get; set; } //i made mistakes before.  so would like the possibility that it would autouse (if not in use).
    public AdvancedCraftingSettings Crafting { get; set; } = new();
    public bool AutomateWorksiteCollection { get; set; }
    public bool AutomateCropCollection { get; set; }
    public EnumAnimalCollectionMode AnimalCollectionPolicy { get; set; } = EnumAnimalCollectionMode.AllAtOnce;
    public bool CollectAllAvailableFromTrees { get; set; } = true;
    public EnumAnimalDefaultOption AnimalDefaultMode { get; set; } = EnumAnimalDefaultOption.Manual;
    //public bool AlwaysChooseFastestAnimalOption { get; set; } //if you set to always fastest one, will gave me a step when doing the animals.
    public BasicList<WorksiteWorkerPreferenceModel> WorksiteWorkerPreferences { get; set; } = [];
    public BasicList<string> GetPreferredWorkersForWorksite(string location)
    {
        var match = WorksiteWorkerPreferences.SingleOrDefault(x => x.WorksiteLocation == location);
        return match?.PreferredWorkers?.ToBasicList() ?? [];
    }

    public void SetPreferredWorkersForWorksite(string location, BasicList<string> workersInOrder)
    {
        var match = WorksiteWorkerPreferences.SingleOrDefault(x => x.WorksiteLocation == location);
        if (match is null)
        {
            WorksiteWorkerPreferences.Add(new()
            {
                WorksiteLocation = location,
                PreferredWorkers = workersInOrder.ToBasicList()
            });
        }
        else
        {
            match.PreferredWorkers = workersInOrder.ToBasicList();
        }
    }
}
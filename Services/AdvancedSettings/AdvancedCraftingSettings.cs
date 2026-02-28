namespace FarmSim.Domain.Services.AdvancedSettings;
public class AdvancedCraftingSettings
{
    public bool EnableBulkCraftButton { get; set; }
    public bool AutoUseFinishResources { get; set; }
    public EnumWorkshopCollectionCategories WorkshopCollectionPolicy { get; set; } = EnumWorkshopCollectionCategories.FullyManual;
    public AdvancedCraftingSettings Clone() => new()
    {
        EnableBulkCraftButton = EnableBulkCraftButton,
        AutoUseFinishResources = AutoUseFinishResources,
        WorkshopCollectionPolicy = WorkshopCollectionPolicy
    };
}
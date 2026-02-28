namespace FarmSim.Domain.Services.Crops;
public class CropUpgradeTierRule
{
    public string CropName { get; set; } = "";  // same key as recipe.Item
    public BasicList<int> TierLevels { get; set; } = [];
}
namespace FarmSim.Domain.Services.Abstractions;
public interface ICropManager
{
    TimeSpan GetTimeForGivenCrop(string name);
    void SetCropSuppressionByProducedItem(string itemName, bool supressed);
    void ApplyCropProgressionUnlocks(CropProgressionPlanModel plan, int level);
    void GrantUnlimitedCropItems(GrantableItem item);

}
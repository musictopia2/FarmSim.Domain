namespace FarmSim.Domain.Services.Crops;
public class CropServicesContext
{
    required public ICropRecipes CropRecipes { get; init; }
    required public ICropRepository CropRepository { get; init; }
    required public ICropUpgradeTierRules CropUpgradeTierRules { get; init; }
}
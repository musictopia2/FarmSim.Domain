namespace FarmSim.Domain.Services.Balance;
public class BaseBalanceProfile
{
    required public double CropTimeMultiplier { get; set; } = 1.0; //this is standard.
    required public double AnimalTimeMultiplier { get; set; } = 1.0;
    required public double WorkshopTimeMultiplier { get; set; } = 1.0;
    required public double TreeTimeMultiplier { get; set; } = 1.0;
    required public double WorksiteTimeMultiplier { get; set; } = 1.0;
}       
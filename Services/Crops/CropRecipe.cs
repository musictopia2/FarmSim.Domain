namespace FarmSim.Domain.Services.Crops;
public class CropRecipe
{
    public string Item { get; init; } = "";
    public TimeSpan Duration { get; init; }
    //required public BasicList<int> TierLevelRequired { get; init; } = [];
    required public bool IsFast { get; init; }
    required public int HowMany { get; init;  } //this is now required to accomodate the automated world.
}
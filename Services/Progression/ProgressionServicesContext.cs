namespace FarmSim.Domain.Services.Progression;
public class ProgressionServicesContext
{
    required public ILevelProgressionPlanProvider LevelProgressionPlanProvider { get; init; } //used so when i level up, decide what i can now get.
    required public ICropProgressionPlanProvider CropProgressionPlanProvider { get; init; }
    required public IAnimalProgressionPlanProvider AnimalProgressionPlanProvider { get; init;  }
    required public IWorkshopProgressionPlanProvider WorkshopProgressionPlanProvider { get; init; }
    required public IProgressionProfile ProgressionProfile { get; init; }
    //all other services goes here.

}
namespace FarmSim.Domain.Services.Automation.Workshops;
public interface IWorkshopAutomationProfile
{
    Task<BasicList<WorkshopAutomationBuildingLaneStateModel>> LoadAsync();
    Task SaveAsync(BasicList<WorkshopAutomationBuildingLaneStateModel> items);
}
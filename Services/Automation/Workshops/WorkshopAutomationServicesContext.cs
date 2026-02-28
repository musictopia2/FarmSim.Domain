namespace FarmSim.Domain.Services.Automation.Workshops;
public class WorkshopAutomationServicesContext
{
    required public IWorkshopAutomationProfile WorkshopAutomationProfile { get; init; }
    required public IWorkshopBaseRulesProvider WorkshopBaseRulesProvider { get; init; }
    required public IWorkshopRecipes WorkshopRecipes { get; init; }
}
namespace FarmSim.Domain.Services.Automation.Workshops;
public interface IWorkshopAutomationFactory
{
    WorkshopAutomationServicesContext GetWorkshopServices(FarmKey farm);
}
namespace FarmSim.Domain.Services.Automation.Worksites;
public interface IWorksiteAutomationFactory
{
    WorksiteAutomationServicesContext GetWorksiteServices(FarmKey farm);
}
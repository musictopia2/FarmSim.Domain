namespace FarmSim.Domain.Services.Automation.Trees;
public interface ITreeAutomationFactory
{
    TreeAutomationServicesContext GetTreeServices(FarmKey farm);
}
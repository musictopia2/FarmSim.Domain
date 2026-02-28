namespace FarmSim.Domain.Services.Automation.Worksites;
public interface IWorksiteAutomationProfile
{
    Task<BasicList<WorksiteAutomationStateModel>> LoadAsync();
    Task SaveAsync(BasicList<WorksiteAutomationStateModel> worksites);
}
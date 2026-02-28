namespace FarmSim.Domain.Services.Automation.Trees;
public interface ITreeAutomationProfile
{
    Task<BasicList<TreeAutomationStateModel>> LoadAsync();
    Task SaveAsync(BasicList<TreeAutomationStateModel> trees);
}
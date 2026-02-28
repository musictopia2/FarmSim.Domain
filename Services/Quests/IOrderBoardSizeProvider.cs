namespace FarmSim.Domain.Services.Quests;
public interface IOrderBoardSizeProvider
{
    Task<int> GetBoardSizeAsync(FarmKey farm, bool isAutomated);
}
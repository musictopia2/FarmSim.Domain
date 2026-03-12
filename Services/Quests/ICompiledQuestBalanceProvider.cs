namespace FarmSim.Domain.Services.Quests;
public interface ICompiledQuestBalanceProvider
{
    Task<BasicList<CompiledQuestItemRow>> GetQuestBalanceAsync(FarmKey farm, bool automated);
}
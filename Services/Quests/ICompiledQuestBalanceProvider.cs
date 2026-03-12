namespace FarmSim.Domain.Services.Quests;
public interface ICompiledQuestBalanceProvider
{
    Task<BasicList<CompiledQuestItemRowModel>> GetQuestBalanceAsync(FarmKey farm, bool automated);
}
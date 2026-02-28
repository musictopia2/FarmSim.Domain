namespace FarmSim.Domain.Services.Automation.Workshops;
public interface IWorkshopBaseRulesProvider
{
    // cap per OUTPUT ITEM (flour, sugar, etc.)
    Task<BasicList<WorkshopItemBaseRuleModel>> GetRulesAsync(FarmKey farm);
}
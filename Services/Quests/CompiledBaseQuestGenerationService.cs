namespace FarmSim.Domain.Services.Quests;
public class CompiledBaseQuestGenerationService : IQuestGenerationService
{
    QuestInstanceModel IQuestGenerationService.CreateQuest(int currentLevel,
        BasicList<QuestRewardRow> rewards,
        BasicList<CompiledQuestItemRow> allItems,
        BasicList<CategoryWeightRow> categories
        )
    {
        var category = ChooseCategory(categories, currentLevel);
        var filters = allItems.ToBasicList();
        filters.KeepConditionalItems(x => x.PlayerLevel == currentLevel && x.ItemCategory == category);
        CompiledQuestItemRow chosen = GetChosenItem(filters);
        int required = Required(chosen);
        return new()
        {
            ItemName = chosen.ItemName,
            LevelRequired  = currentLevel,
            Rewards = GetReward(rewards, currentLevel),
            Required = required,
        };
    }
    private static Dictionary<string, int> GetReward(BasicList<QuestRewardRow> rewards, int currentLevel)
    {
        QuestRewardRow? row = rewards.SingleOrDefault(x =>
            currentLevel >= x.MinLevel &&
            (x.MaxLevel == null || currentLevel <= x.MaxLevel)
            );

        if (row == null)
        {
            throw new CustomBasicException($"No reward rule found for level {currentLevel}.");
        }

        return row.Rewards;
    }
    private static int Required(CompiledQuestItemRow item)
    {
        return item.Ranges.GetRandomItem();
    }
    private static CompiledQuestItemRow GetChosenItem(BasicList<CompiledQuestItemRow> compileList)
    {
        BasicList<string> possList = [];
        foreach (var item in compileList)
        {
            item.ItemWeight.Times(() =>
            {
                possList.Add(item.ItemName);
            });
        }
        string doChoose = possList.GetRandomItem();
        return compileList.Single(x => x.ItemName == doChoose);
    }
    private static EnumItemCategory ChooseCategory(BasicList<CategoryWeightRow> categories, int level)
    {
        var category = categories.Single(x => x.PlayerLevel == level);

        BasicList<EnumItemCategory> poss = [];
        category.CropTreeWeight.Times(() =>
        {
            poss.Add(EnumItemCategory.Crop);
        });
        if (category.WorkshopWeight > 0)
        {
            poss.Add(EnumItemCategory.Workshop);
        }
        if (category.WorksiteWeight > 0)
        {
            poss.Add(EnumItemCategory.Worksite);
        }
        if (category.AnimalWeight > 0)
        {
            poss.Add(EnumItemCategory.Animal);
        }
        EnumItemCategory output = poss.GetRandomItem();
        return output;
    }
}
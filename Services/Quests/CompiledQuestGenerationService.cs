namespace FarmSim.Domain.Services.Quests;
public class CompiledQuestGenerationService : IQuestGenerationService
{
    QuestInstanceModel IQuestGenerationService.CreateQuest(int currentLevel,
        BasicList<QuestRewardRowModel> rewards,
        BasicList<CompiledQuestItemRowModel> allItems,
        BasicList<CategoryWeightRowModel> categories
        )
    {
        var category = ChooseCategory(categories, currentLevel);
        var filters = allItems.ToBasicList();
        filters.KeepConditionalItems(x => x.PlayerLevel == currentLevel && x.ItemCategory == category);
        CompiledQuestItemRowModel chosen = GetChosenItem(filters);
        int required = Required(chosen);
        return new()
        {
            ItemName = chosen.ItemName,
            LevelRequired  = currentLevel,
            Rewards = GetReward(rewards, currentLevel),
            Required = required,
        };
    }
    private static Dictionary<string, int> GetReward(BasicList<QuestRewardRowModel> rewards, int currentLevel)
    {
        QuestRewardRowModel? row = rewards.SingleOrDefault(x =>
            currentLevel >= x.MinLevel &&
            (x.MaxLevel == null || currentLevel <= x.MaxLevel)
            );

        if (row == null)
        {
            throw new CustomBasicException($"No reward rule found for level {currentLevel}.");
        }

        return row.Rewards;
    }
    private static int Required(CompiledQuestItemRowModel item)
    {
        return item.Ranges.GetRandomItem();
    }
    private static CompiledQuestItemRowModel GetChosenItem(BasicList<CompiledQuestItemRowModel> compileList)
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
    private static EnumItemCategory ChooseCategory(BasicList<CategoryWeightRowModel> categories, int level)
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
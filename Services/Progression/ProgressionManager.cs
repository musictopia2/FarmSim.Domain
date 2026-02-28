namespace FarmSim.Domain.Services.Progression;
public class ProgressionManager(InventoryManager inventoryManager,
    ICropManager cropManager,
    IAnimalManager animalManager,
    ITreeManager treeManager,
    IWorkshopManager workshopManager,
    IWorksiteManager worksiteManager,
    CatalogManager catalogManager,
    RulesManager rulesManager
    ) : IFarmProgressionReadOnly
{
    private LevelProgressionPlanModel _levelPlan = null!;
    private CropProgressionPlanModel _cropPlan = null!;
    private BasicList<ItemUnlockRule> _animalPlan = null!;
    private BasicList<CatalogOfferModel> _animalOffers = null!;
    private BasicList<CatalogOfferModel> _treesOffers = null!;
    private BasicList<CatalogOfferModel> _worksiteOffers = null!;
    private BasicList<CatalogOfferModel> _workerOffers = null!;
    private BasicList<ItemUnlockRule> _workshopPlan = null!;
    private BasicList<CatalogOfferModel> _workshopOffers = null!;
    private ProgressionProfileModel _currentProfile = null!;
    private IProgressionProfile _profileService = null!;
    public event Action? Changed;
    public event Action<int>? OnIncreaseLevel; //return the new level.
    private void NotifyChanged() => Changed?.Invoke();
    public async Task SetProgressionStyleContextAsync(ProgressionServicesContext context,
        FarmKey farm)
    {
        _levelPlan = await context.LevelProgressionPlanProvider.GetPlanAsync(farm, rulesManager.AutomationEnabled);
        _currentProfile = await context.ProgressionProfile.LoadAsync();
        _profileService = context.ProgressionProfile;
        _cropPlan = await context.CropProgressionPlanProvider.GetPlanAsync(farm);
        _animalPlan = await context.AnimalProgressionPlanProvider.GetPlanAsync(farm);
        _treesOffers = catalogManager.GetFreeOffers(EnumCatalogCategory.Tree);
        _animalOffers = catalogManager.GetFreeOffers(EnumCatalogCategory.Animal);
        _worksiteOffers = catalogManager.GetFreeOffers(EnumCatalogCategory.Worksite);
        _workerOffers = catalogManager.GetFreeOffers(EnumCatalogCategory.Worker);
        _workshopOffers = catalogManager.GetFreeOffers(EnumCatalogCategory.Workshop);
        _workshopPlan = await context.WorkshopProgressionPlanProvider.GetPlanAsync(farm);
    }
    public int CurrentLevel => _currentProfile.Level;
    public int NextLevel => _currentProfile.CompletedGame ? 0 : _currentProfile.Level + 1;
    public int CurrentPoints => _currentProfile.PointsThisLevel;
    public int PointsRequired
    {
        get
        {
            var profile = GetCurrentTier();
            return profile.RequiredPoints;
        }
    }

    public bool IsEnd(int levelDesired)
    {
        if (_levelPlan.IsEndless)
        {
            return false;
        }
        if (levelDesired <= _levelPlan.Tiers.Count)
        {
            return false;
        }
        return true;
    }

    public int PreviewLevelPoints(int levelDesired)
    {
        //-1 means game is over.

        if (levelDesired > _levelPlan.Tiers.Count && _levelPlan.IsEndless == false)
        {
            return -1; //its over.
        }
        if (levelDesired > _levelPlan.Tiers.Count)
        {
            return _levelPlan.Tiers.Last().RequiredPoints;
        }
        return _levelPlan.Tiers[levelDesired - 1].RequiredPoints;
    }

    public bool CompletedGame => _currentProfile.CompletedGame;
    public async Task IncrementLevelAsync(FarmKey farm, FarmTransferService transfer)
    {
        if (CompletedGame)
        {
            return;
        }
        if (_levelPlan.IsEndless)
        {
            RewardEndOfLevel();
            _currentProfile.Level++;
            OnIncreaseLevel?.Invoke(_currentProfile.Level);
            await ProcessUnlocksAsync();
            await SaveAsync(farm, transfer);
            return;
        }
        if (_currentProfile.Level >= _levelPlan.Tiers.Count)
        {
            RewardEndOfLevel(); //if you ended game, no need to unlock anything.
            _currentProfile.CompletedGame = true;
            await SaveAsync(farm, transfer);
            return;
        }
        RewardEndOfLevel();
        _currentProfile.Level++;
        OnIncreaseLevel?.Invoke(_currentProfile.Level);
        await ProcessUnlocksAsync();
        await SaveAsync(farm, transfer);
    }

    public int LevelForCraftedItem(string item) => _workshopPlan.Single(x => x.ItemName == item).LevelRequired;
    public BasicList<string> GetCropPreviewOfNextLevel() //so can show up on crops page.
    {
        if (_currentProfile.CompletedGame)
        {
            return [];
        }
        BasicList<string> output = [];
        int nextLevel = _currentProfile.Level + 1;
        _cropPlan.UnlockRules.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.ItemName);
        });
        return output;
    }
    public BasicList<string> GetWorkshopPreviewOfNextLevel(string buildingName)
    {
        if (_currentProfile.CompletedGame)
        {
            return [];
        }
        BasicList<string> output = [];
        int nextLevel = _currentProfile.Level + 1;
        _workshopPlan.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            if (workshopManager.IsInBuilding(buildingName, item.ItemName))
            {
                output.Add(item.ItemName);
            }
        });
        return output;
    }
    private BasicList<string> GetFullPreviewOfNextLevel()
    {
        if (_currentProfile.CompletedGame)
        {
            return [];
        }
        BasicList<string> output = [];
        int nextLevel = _currentProfile.Level + 1;
        //this only shows items that are free anyways.
        _treesOffers.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.TargetName);
        });
        _animalOffers.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.TargetName);
        });
        _workshopOffers.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.TargetName);
        });
        _workshopPlan.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.ItemName);
        });
        _worksiteOffers.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.TargetName);
        });
        _workerOffers.ForConditionalItems(x => x.LevelRequired == nextLevel, item =>
        {
            output.Add(item.TargetName);
        });
        var fins = GetCropPreviewOfNextLevel();
        fins.ForEach(output.Add);
        return output;
    }
    private int GetNewSlotsPreviewOfNextLevel
    {
        get
        {
            if (_currentProfile.CompletedGame)
            {
                return 0;
            }
            int nextLevel = _currentProfile.Level + 1;
            return _cropPlan.SlotLevelRequired.Count(x => x == nextLevel);
        }
    }
    public async Task UpdateCoopProfileAsync(ProgressionProfileModel profile)
    {
        bool newLevel = false;
        if (profile.Level > _currentProfile.Level)
        {
            newLevel = true;
        }
        _currentProfile = profile;
        if (newLevel)
        {
            await ProcessUnlocksAsync();
        }
        await SaveAsync();
    }
    private async Task ProcessUnlocksAsync()
    {
        cropManager.ApplyCropProgressionUnlocks(_cropPlan, _currentProfile.Level); //new level.
        animalManager.ApplyAnimalProgressionUnlocksFromLevels(_animalPlan, _animalOffers, _currentProfile.Level);
        treeManager.ApplyTreeUnlocksOnLevels(_treesOffers, _currentProfile.Level);
        workshopManager.ApplyWorkshopProgressionOnLevelUnlocks(_workshopPlan, _workshopOffers, _currentProfile.Level);
        worksiteManager.ApplyWorksiteProgressionUnlocksFromLevels(_worksiteOffers, _currentProfile.Level);
        await worksiteManager.ApplyWorkerProgressionUnlocksFromLevelsAsync(_workerOffers, _currentProfile.Level);
    }
    private LevelProgressionTier GetCurrentTier()
    {
        if (_levelPlan.Tiers.Count == 0)
        {
            return new();
        }
        if (_currentProfile.Level - 1 >= _levelPlan.Tiers.Count)
        {
            return _levelPlan.Tiers.Last();
        }
        return _levelPlan.Tiers[_currentProfile.Level - 1];
    }
    private async Task SaveAsync(FarmKey farm, FarmTransferService transfer)
    {
        if (farm.IsMain == false || rulesManager.AutomationEnabled)
        {
            await SaveAsync();
            return;
        }
        await transfer.UpdateCoopProgressionAsync(farm, _currentProfile);
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        await _profileService.SaveAsync(_currentProfile);
        NotifyChanged();
    }

    public AnimalPreviewOption? NextAnimalOption(string animal)
    {
        if (_currentProfile.CompletedGame)
        {
            return null;
        }
        var nexts = _animalPlan.FirstOrDefault(x => x.ItemName == animal && x.LevelRequired > _currentProfile.Level);
        if (nexts is null)
        {
            return null;
        }
        var option = animalManager.NextProductionOption(animal);
        return new()
        {
            Option = option,
            Level = nexts.LevelRequired
        };
    }


    public ItemUnlockRule? NextCrop
    {
        get
        {
            if (_currentProfile.CompletedGame)
            {
                return null;
            }
            return _cropPlan.UnlockRules.FirstOrDefault(x => x.LevelRequired > _currentProfile.Level);
        }
    }
    private void RewardEndOfLevel()
    {
        _currentProfile.PointsThisLevel = 0;
        LevelProgressionTier tier = GetCurrentTier();

        inventoryManager.Add(tier.RewardsOnLevelComplete);
        //has to figure out how to communicate with the crop manager to get the data.
        //well see how this can work (?)
        //refer to how i upgraded workshop capacity for ideas.


    }
    private static int GetThresholdPoint(int requiredPoints, int percent)
    {
        if (percent <= 0 || percent >= 100)
        {
            throw new CustomBasicException("Milestone percent must be between 1 and 99.");
        }
        return (int)Math.Ceiling(requiredPoints * (percent / 100.0));
    }
    public string GetNextRewardDetails()
    {
        if (CompletedGame)
        {
            return "";
        }

        LevelProgressionTier tier = GetCurrentTier();
        int required = tier.RequiredPoints;
        int current = _currentProfile.PointsThisLevel;

        if (required <= 0)
        {
            return "";
        }

        // If no milestone rewards exist, next reward is level-up
        if (tier.ProgressMilestoneRewards is null || tier.ProgressMilestoneRewards.Count == 0)
        {
            int toLevel = Math.Max(0, required - current);
            return $"";
        }

        // Build ordered thresholds (lowest -> highest)
        var thresholds = tier.ProgressMilestoneRewards
            .Select(m => GetThresholdPoint(required, m.Percent))
            .Distinct()
            .OrderBy(x => x)
            .ToBasicList();

        // Next milestone is the smallest threshold strictly greater than current points
        int? nextMilestone = thresholds.FirstOrDefault(t => t > current);

        if (nextMilestone.HasValue && nextMilestone.Value > 0)
        {
            int toMilestone = nextMilestone.Value - current;
            return $"{toMilestone:N0} more points to next milestone reward";
        }

        // All milestones are already passed; next reward is level-up
        int toNextLevel = Math.Max(0, required - current);
        return $"";
    }
    public int PointsNeededToLevelUp
    {
        get
        {
            LevelProgressionTier tier = GetCurrentTier();
            return tier.RequiredPoints - _currentProfile.PointsThisLevel;
        }
    }


    public Dictionary<string, int> GetMilestoneRewards()
    {
        //may even be 0 (if no more left).
        LevelProgressionTier tier = GetCurrentTier();
        //var list = tier.ProgressMilestoneRewards.ord(x => x.Percent).ToBasicList();
        foreach (var milestone in tier.ProgressMilestoneRewards)
        {
            int thresholdPoint = GetThresholdPoint(tier.RequiredPoints, milestone.Percent);

            if (thresholdPoint > _currentProfile.PointsThisLevel)
            {
                return milestone.Rewards;
            }

            // Award ONLY if this point EXACTLY hits the threshold
            //if (_currentProfile.PointsThisLevel == thresholdPoint)
            //{
            //    inventoryManager.Add(milestone.Rewards);
            //    break; // guarantee only one milestone per point
            //}
        }
        return [];
    }
    public Dictionary<string, int> GetLevelRewards()
    {
        Dictionary<string, int> output = [];
        BasicList<string> unlocks = GetFullPreviewOfNextLevel();
        foreach (var item in unlocks)
        {
            output.Add(item, 0);
        }
        int slots = GetNewSlotsPreviewOfNextLevel;
        //not sure how to handle the part where there is a building that i cannot quite get (even though free).

        if (slots > 0)
        {
            output.Add("Crop", slots);
        }
        LevelProgressionTier tier = GetCurrentTier();
        var others = tier.RewardsOnLevelComplete;

        foreach (var item in others)
        {
            output.Add(item.Key, item.Value);
        }
        return output;
    }
    public BasicList<int> GetCompleteThresholds()
    {
        LevelProgressionTier tier = GetCurrentTier();
        BasicList<int> output = [];
        foreach (var item in tier.ProgressMilestoneRewards)
        {
            output.Add(GetThresholdPoint(tier.RequiredPoints, item.Percent));
        }
        return output;
    }
    public async Task AddPointSinglePointAsync(FarmKey farm, FarmTransferService transfer)
    {
        if (CompletedGame)
        {
            return;
        }
        LevelProgressionTier tier = GetCurrentTier();
        // 1. Increment by exactly one
        _currentProfile.PointsThisLevel++;

        // 2. Check for level completion FIRST
        if (_currentProfile.PointsThisLevel >= tier.RequiredPoints)
        {
            await IncrementLevelAsync(farm, transfer);
            return;
        }
        // 3. Otherwise, check milestone rewards
        if (tier.ProgressMilestoneRewards.Count == 0)
        {
            await SaveAsync(farm, transfer);
            return;
        }
        var list = tier.ProgressMilestoneRewards.OrderByDescending(x => x.Percent).ToBasicList();
        foreach (var milestone in list)
        {
            int thresholdPoint = GetThresholdPoint(tier.RequiredPoints, milestone.Percent);

            // Award ONLY if this point EXACTLY hits the threshold
            if (_currentProfile.PointsThisLevel == thresholdPoint)
            {
                inventoryManager.Add(milestone.Rewards);
                break; // guarantee only one milestone per point
            }
        }

        // 4. Persist profile
        await SaveAsync(farm, transfer);
    }
}
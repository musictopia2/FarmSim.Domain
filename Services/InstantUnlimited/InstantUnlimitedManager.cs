namespace FarmSim.Domain.Services.InstantUnlimited;
public class InstantUnlimitedManager(ICropManager cropManager,
    ITreeManager treeManager, IAnimalManager animalManager,
    InventoryManager inventoryManager,
    ItemManager itemManager
    )
{
    private BasicList<UnlockModel> _items = [];
    private IInstantUnlimitedProfile _profile = null!;
    public event Action? Changed;
    public event Action<string>? OnUpgradedToInstantUnlimited;
    public BasicList<string> GetInstantItems => _items.Where(x => x.Unlocked).Select(x => x.Name).ToBasicList();
    public async Task SetInstantUnlimitedStyleContextAsync(InstantUnlimitedServicesContext context)
    {
        _items = await context.InstantUnlimitedProfile.LoadAsync();
        _profile = context.InstantUnlimitedProfile;
    }
    public async Task DoubleCheckActiveRentalAsync(string item)
    {
        await SetLockStateAsync(item, true);
    }
    public async Task<bool> CanDeleteRentalAsync(string item)
    {
        var model = _items.SingleOrDefault(x => x.Name == item) ?? throw new CustomBasicException($"Instant Unlimited item '{item}' was not found.");
        if (model.Unlocked == false)
        {
            return true;
        }
        await SetLockStateAsync(item, false);
        return false;
    }
    public async Task UpgradeToInstantUnlimitedAsync(string item)
    {
        if (itemManager.ItemExists(item) == false)
        {
            return; //does not even exist.  means no instant unlimited period.
        }
        var unlock = _items.SingleOrDefault(x => x.Name == item);
        if (unlock is null)
        {
            unlock = new()
            {
                Name = item,
                Unlocked = false //set to false so can run the rest of the proceses
            };
            _items.Add(unlock);
        }
        OnUpgradedToInstantUnlimited?.Invoke(item);
        await SetLockStateAsync(item, true);
    }
    public async Task SetLockStateAsync(string item, bool unlocked)
    {
        var model = _items.SingleOrDefault(x => x.Name == item) ?? throw new CustomBasicException($"Instant Unlimited item '{item}' was not found.");
        if (model.Unlocked == unlocked)
        {
            return; // no-op
        }
        model.Unlocked = unlocked;
        //has to do other things too.
        EnumItemCategory category = itemManager.GetItemCategory(item);
        if (category == EnumItemCategory.Tree)
        {
            //must lock up a tree
            treeManager.SetTreeSuppressionByProducedItem(item, unlocked);
        }
        else if (category == EnumItemCategory.Crop)
        {
            cropManager.SetCropSuppressionByProducedItem(item, unlocked);
        }
        else if (category == EnumItemCategory.Animal)
        {
            //must lock up an animal
            animalManager.SetAnimalSuppressionByProducedItem(item, unlocked);
        }
        else
        {
            throw new CustomBasicException("Not supported");
        }
        Changed?.Invoke();
        await _profile.SaveAsync(_items);
    }
    public BasicList<string> UnlockedInstances =>
        _items.Where(x => x.Unlocked).Select(x => x.Name).ToBasicList();

    public bool IsUnlocked(string item) =>
        _items.Any(x => x.Unlocked && x.Name == item);

    public bool CanApplyInstantUnlimited(string item, int howMany)
    {
        if (howMany <= 0)
        {
            return false;
        }

        if (IsUnlocked(item) == false)
        {
            return false;
        }

        var category = itemManager.GetItemCategory(item);

        // Only allow categories you actually support
        if (category is not (EnumItemCategory.Tree or EnumItemCategory.Crop or EnumItemCategory.Animal))
        {
            return false;
        }

        return inventoryManager.CanAdd(item, howMany);
    }
    public void ApplyInstantUnlimited(string item, int howMany)
    {
        if (CanApplyInstantUnlimited(item, howMany) == false)
        {
            throw new CustomBasicException("Should had used CanApplyInstantUnlimited");
        }
        //has to figure out which to use.
        EnumItemCategory category = itemManager.GetItemCategory(item);
        GrantableItem grant = new()
        {
            Amount = howMany,
            Item = item,
            Category = category,
            Source = itemManager.GetSource(item)
        };
        switch (category)
        {
            case EnumItemCategory.Tree:
                treeManager.GrantUnlimitedTreeItems(grant);
                return;

            case EnumItemCategory.Crop:
                cropManager.GrantUnlimitedCropItems(grant);
                return;

            case EnumItemCategory.Animal:
                animalManager.GrantUnlimitedAnimalItems(grant);
                return;

            default:
                // should be unreachable due to CanApply, but keep it defensive
                throw new CustomBasicException("Only trees, crops and animals are supported");
        }
    }
}
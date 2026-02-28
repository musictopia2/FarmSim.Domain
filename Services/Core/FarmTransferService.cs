namespace FarmSim.Domain.Services.Core;
public class FarmTransferService(GameRegistry gameRegistry)
{
    public bool CanTransferInventory(FarmKey farm, RulesManager rulesManager, ItemAmount item)
    {
        if (farm.IsCoin)
        {
            return false;
        }
        if (rulesManager.RuleChosen == EnumRuleCategory.Automated)
        {
            return false; //automated options can never transfer inventory.
        }
        FarmKey other;
        if (farm.IsMain)
        {
            other = farm.AsCooperative;
        }
        else if (farm.IsCooperative)
        {
            other = farm.AsMain;
        }
        else
        {
            return false;
        }

        var container = gameRegistry.GetFarm(other);
        return container.InventoryManager.CanAdd(item);
    }

    public void TransferInventory(FarmKey farm, RulesManager rulesManager, ItemAmount item, InventoryManager original)
    {
        if (CanTransferInventory(farm, rulesManager, item) == false)
        {
            throw new CustomBasicException("Unable to transfer inventory.  Should had called CanTransferInventory");
        }
        FarmKey other;
        if (farm.IsMain)
        {
            other = farm.AsCooperative;
        }
        else if (farm.IsCooperative)
        {
            other = farm.AsMain;
        }
        else
        {
            throw new CustomBasicException("Wrong farm");
        }
        var container = gameRegistry.GetFarm(other);
        container.InventoryManager.Add(item);
        original.Consume(item);
    }
    public async Task AcquireForCoopAsync(FarmKey farm, StoreItemRowModel store)
    {
        if (farm.IsMain == false)
        {
            throw new CustomBasicException("Only main can acquire for coop");
        }
        FarmKey other = farm.AsCooperative;
        var container = gameRegistry.GetFarm(other);
        StoreItemRowModel coop = store.Clone();
        await container.StoreManager.AcquireAsync(coop, false);
    }
    public async Task UpdateCoopProgressionAsync(FarmKey farm, ProgressionProfileModel profile)
    {
        if (farm.IsMain == false)
        {
            throw new CustomBasicException("Only main can update the progression of coop");
        }
        FarmKey other = farm.AsCooperative;
        var container = gameRegistry.GetFarm(other);
        ProgressionProfileModel coop = profile.Clone();
        await container.ProgressionManager.UpdateCoopProfileAsync(coop);
    }
    public void UpdateCoopWorkshopCapacity(FarmKey farm, WorkshopView workshop, int capacity)
    {
        if (farm.IsMain == false)
        {
            throw new CustomBasicException("Only main can update the workshop of coop");
        }
        FarmKey other = farm.AsCooperative;
        var container = gameRegistry.GetFarm(other);
        container.WorkshopManager.UpdateCapacity(workshop, capacity);
    }
    public async Task UpdateCoopStorageAsync(FarmKey farm, InventoryStorageProfileModel profile)
    {
        if (farm.IsMain == false)
        {
            throw new CustomBasicException("Only main can update the storage of coop");
        }
        InventoryStorageProfileModel coop = profile.Clone();
        FarmKey other = farm.AsCooperative;
        var container = gameRegistry.GetFarm(other);
        await container.UpgradeManager.UpdateCoopAsync(coop);
    }
    public async Task AddCoinFromScenarioCompletionAsync(FarmKey coinFarm, int amount, IToast toast)
    {
        if (!coinFarm.IsCoin)
        {
            throw new InvalidOperationException("Coin can only be transferred from coin farms.");
        }
        FarmKey other = coinFarm.AsMain;
        //this will not actually remove inventory because never put into inventory (if i do put into inventory, then would remove then).
        if (amount <= 0)
        {
            return;
        }
        var temps = gameRegistry.GetFarm(other);
        int firsts = await temps.AchievementManager.ScenarioCompletedAsync();
        int earned = temps.AchievementManager.CoinsEarnedFromAchievement(amount + firsts);
        bool neededToast = false;
        if (earned > 0)
        {
            toast.ShowSuccessToast($"You earned at least one achievement for earnings coins.   You earned at least {earned} coins.");
            neededToast = true;
        }
        if (firsts > 0)
        {
            toast.ShowSuccessToast($"You earned {firsts} from completing scenarios");
            neededToast = true;
        }
        if (neededToast)
        {
            await Task.Delay(2000); //so you can see the toasts for 2 seconds.
        }
        temps.InventoryManager.AddCoin(amount);
    }
}
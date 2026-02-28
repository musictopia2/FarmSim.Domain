namespace FarmSim.Domain.Services.Rentals;
public class RentalManager(TreeManager? treeManager,
    AnimalManager? animalManager, WorkshopManager? workshopManager,
    IWorksiteManager worksiteManager,
    InstantUnlimitedManager instantUnlimitedManager,
    RulesManager rulesManager
    )
{
    private BasicList<RentalInstanceModel> _rentals = [];
    private IRentalProfile _profile = null!;
    public async Task SetRentalStyleContextAsync(RentalsServicesContext context)
    {
        _rentals = await context.RentalProfile.LoadAsync();
        _profile = context.RentalProfile;
        await RentalProgressAsync();
        await SaveAsync();
        // reset cache when loading
    }
    public bool CanRent(StoreItemRowModel row)
    {
        if (row.Category != EnumCatalogCategory.Worker && rulesManager.AutomationEnabled && row.Category != EnumCatalogCategory.InstantUnlimited)
        {
            return false;
        }
        return _rentals.Exists(x => x.TargetName == row.TargetName) == false;
    }
    public async Task RentAsync(StoreItemRowModel row)
    {
        if (rulesManager.AutomationEnabled && row.Category == EnumCatalogCategory.Worker)
        {

            await worksiteManager.UnlockWorkerAcquiredAsync(row); //workers don't need ids.
            AddEntry(row, null);
            return;
        }
        if (rulesManager.AutomationEnabled && row.Category == EnumCatalogCategory.InstantUnlimited)
        {
            await instantUnlimitedManager.SetLockStateAsync(row.TargetName, true);
            AddEntry(row, null);
            return;
        }
        if (rulesManager.AutomationEnabled)
        {
            throw new CustomBasicException("Cannot rent because automation mode");
        }
        if (treeManager is null)
        {
            throw new CustomBasicException("TreeManager is null while rentals are enabled (manual mode). Initialization bug.");
        }
        if (animalManager is null)
        {
            throw new CustomBasicException("AnimalManager is null while rentals are enabled (manual mode). Initialization bug.");
        }
        if (workshopManager is null)
        {
            throw new CustomBasicException("WorkshopManager is null while rentals are enabled (manual mode). Initialization bug.");
        }
        if (CanRent(row) == false)
        {
            throw new CustomBasicException("Unable to rent because already renting.  Should had checked CanRent");
        }
        
        Guid? id = null;
        if (row.Category == EnumCatalogCategory.Tree)
        {
            id = treeManager.StartRental(row);
        }
        else if (row.Category == EnumCatalogCategory.Animal)
        {
            id = animalManager.StartRental(row);
        }
        else if (row.Category == EnumCatalogCategory.Workshop)
        {
            id = workshopManager.StartRental(row);
        }
        else if (row.Category == EnumCatalogCategory.Worker)
        {
            await worksiteManager.UnlockWorkerAcquiredAsync(row); //workers don't need ids.
        }
        else if (row.Category == EnumCatalogCategory.InstantUnlimited)
        {
            await instantUnlimitedManager.SetLockStateAsync(row.TargetName, true);
        }
        else
        {
            throw new CustomBasicException("Category not supported");
        }
        AddEntry(row, id);
    }
    private void AddEntry(StoreItemRowModel row, Guid? id)
    {
        if (row.Duration is null)
        {
            throw new CustomBasicException("Rentals require a duration");
        }
        var now = DateTime.Now;
        _rentals.Add(new()
        {
            TargetInstanceId = id,
            Category = row.Category,
            TargetName = row.TargetName,
            StartedAt = now,
            EndsAt = now.Add(row.Duration.Value),
            State = EnumRentalState.Active
        });
        _needsSaving = true;
    }
    public string GetDurationString(string targetName)
    {
        var item = _rentals.SingleOrDefault(x => x.TargetName == targetName);
        if (item is null)
        {
            return "";
        }
        var remaining = item.EndsAt - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return "";
        }
        return remaining.GetTimeCompact;
    }
    public EnumRentalState? GetRentalState(string targetName)
    {
        var item = _rentals.SingleOrDefault(x => x.TargetName == targetName);
        if (item is null)
        {
            return null;
        }
        return item.State;
    }
    private async Task SaveAsync()
    {
        if (_needsSaving == false)
        {
            return;
        }
        await _profile.SaveAsync(_rentals);
        _needsSaving = false;
    }
    private bool _needsSaving = false;
    private async Task RentalProgressAsync()
    {
        var now = DateTime.Now;
        bool changed = false;
        foreach (var item in _rentals.ToBasicList())
        {


            bool isExpiredByTime = item.EndsAt <= now;

            // Move state forward (never backward)
            if (isExpiredByTime && item.State == EnumRentalState.Active)
            {
                item.State = EnumRentalState.ExpiredPending;
                changed = true;
            }

            if (item.Category == EnumCatalogCategory.Tree)
            {
                if (treeManager is null)
                {
                    continue;
                }
                if (item.State == EnumRentalState.Active)
                {
                    treeManager.DoubleCheckActiveRental(item.TargetInstanceId!.Value);
                    // treeManager may set its own _needsSaving; RentalManager only tracks its own here
                }
                else // ExpiredPending
                {
                    // Ensure expired/pending in tree domain, and delete ONLY when domain is finalized
                    bool canDelete = treeManager.CanDeleteRental(item.TargetInstanceId!.Value);
                    if (canDelete)
                    {
                        _rentals.RemoveSpecificItem(item);
                        changed = true;
                    }
                }
            }
            else if (item.Category == EnumCatalogCategory.Animal)
            {
                if (animalManager is null)
                {
                    return;
                }
                // TEMP approach: keep your existing method but do NOT remove record immediately.
                // You should eventually mirror the Tree pattern with id-based methods.
                if (item.State == EnumRentalState.Active)
                {

                    // If you have an "ensure active" method for animals, call it here.
                    animalManager.DoubleCheckActiveRental(item.TargetInstanceId!.Value);
                }
                else
                {
                    // Ensure expired pending in animal domain
                    //await animalManager.ShowRentalExpireAsync(item);
                    bool canDelete = animalManager!.CanDeleteRental(item.TargetInstanceId!.Value, item.TargetName);
                    if (canDelete)
                    {
                        _rentals.RemoveSpecificItem(item);
                        changed = true;
                    }
                    // DO NOT delete until you have a finalized check.
                    // Once you implement animalManager.CanDeleteRental(id), switch to it.
                    // bool canDelete = animalManager.CanDeleteRental(item.TargetInstanceId);
                    // if (canDelete) { _rentals.RemoveSpecificItem(item); changed = true; }
                }
            }
            else if (item.Category == EnumCatalogCategory.Workshop)
            {
                if (workshopManager is null)
                {
                    continue;
                }
                if (item.State == EnumRentalState.Active)
                {
                    workshopManager.DoubleCheckActiveRental(item.TargetInstanceId!.Value);
                }
                else
                {
                    bool canDelete = workshopManager.CanDeleteRental(item.TargetInstanceId!.Value);
                    if (canDelete)
                    {
                        _rentals.RemoveSpecificItem(item);
                        changed = true;
                    }
                }
            }
            else if (item.Category == EnumCatalogCategory.Worker)
            {
                if (item.State == EnumRentalState.Active)
                {
                    await worksiteManager.DoubleCheckActiveWorkerRentalAsync(item);
                }
                else
                {
                    bool canDelete = await worksiteManager.CanDeleteWorkerRentalAsync(item);
                    if (canDelete)
                    {
                        _rentals.RemoveSpecificItem(item);
                        changed = true;
                    }
                }
            }
            else if (item.Category == EnumCatalogCategory.InstantUnlimited)
            {
                if (item.State == EnumRentalState.Active)
                {
                    await instantUnlimitedManager.DoubleCheckActiveRentalAsync(item.TargetName);
                }
                else
                {
                    bool canDelete = await instantUnlimitedManager.CanDeleteRentalAsync(item.TargetName);
                    if (canDelete)
                    {
                        _rentals.RemoveSpecificItem(item);
                        changed = true;
                    }
                }
            }
            else
            {
                // no throw in tick loop; just ignore/log so game doesn't die
                // (unless you're in dev and want to crash fast)
            }
        }

        if (changed)
        {
            _needsSaving = true;
        }
    }
    public async Task UpdateTickAsync()
    {
        if (_rentals.Count == 0)
        {
            return;
        }
        await RentalProgressAsync();
        await SaveAsync();
    }
}
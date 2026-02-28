namespace FarmSim.Domain.Services.Inventory;
public class InventoryManager(FarmKey farm, IInventoryRepository persist,
    ItemRegistry itemRegistry
    )
{
    //does not need to send the profile here yet.
    private readonly Dictionary<string, int> _items = [];
    private InventoryStorageProfileModel _inventoryProfileModel = null!;
    public event Action? InventoryChanged;
    public event Action<ItemAmount>? InventoryAdded;
    public event Action<ItemAmount>? InventoryConsumed;
    public void LoadStartingInventory(Dictionary<string, int> items, InventoryStorageProfileModel storage)
    {
        _inventoryProfileModel = storage;
        ClearInventory(items);
        InventoryChanged?.Invoke(); //not sure but do just in case.
    }
    private void ClearInventory(Dictionary<string, int> items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(item.Key, item.Value);
        }
    }
    public void ResetInventory(Dictionary<string, int> items)
    {
        ClearInventory(items);
        UpdateInventory();
    }
    public void UpdateInventoryProfile(InventoryStorageProfileModel profile)
    {
        _inventoryProfileModel = profile;
    }
    public int CoinCount => Get(CurrencyKeys.Coin);
    public void AddCoin(int amount)
    {
        Add(CurrencyKeys.Coin, amount);
    }
    public void DirectDeposit(int amount)
    {
        FinalAdd(CurrencyKeys.Coin, amount); //no events for this.
        InventoryChanged?.Invoke(); //this is okay though.
    }
    public int BarnSize => _inventoryProfileModel.BarnSize;
    public int SiloSize => _inventoryProfileModel.SiloSize;
    public int Get(string item)
    {
        return _items.GetValueOrDefault(item);
    }
    public bool Has(string item, int amount)
        => Get(item) >= amount;
    public bool Has(Dictionary<string, int> requirements)
        => requirements.All(r => Has(r.Key, r.Value));

    public bool CanAcceptRewards(IEnumerable<ItemAmount> rewards)
    {
        // consolidate duplicates first (optional but safer)
        var consolidated = rewards
            .Where(x => x.Amount > 0)
            .GroupBy(x => x.Item)
            .Select(g => new ItemAmount(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        int barnAdd = 0;
        int siloAdd = 0;

        foreach (var r in consolidated)
        {
            var cat = itemRegistry.Get(r.Item).Storage;

            if (cat == EnumInventoryStorageCategory.None)
            {
                continue; // unlimited / not counted
            }
            else if (cat == EnumInventoryStorageCategory.Barn)
            {
                barnAdd += r.Amount;
            }
            else if (cat == EnumInventoryStorageCategory.Silo)
            {
                siloAdd += r.Amount;
            }
            else
            {
                throw new CustomBasicException($"Unsupported storage category for {r.Item}");
            }
        }

        // compute current sizes once
        int barnCurrent = GetAllBarnInventoryItems().Sum(x => x.Amount);
        int siloCurrent = GetAllSiloInventoryItems().Sum(x => x.Amount);

        return (barnCurrent + barnAdd) <= BarnSize
            && (siloCurrent + siloAdd) <= SiloSize;
    }

    public bool CanAdd(GrantableItem item) => CanAdd(item.Item, item.Amount);
    public bool CanAdd(ItemAmount item) => CanAdd(item.Item, item.Amount);
    public bool CanAdd(string item, int amount)
    {
        EnumInventoryStorageCategory category = itemRegistry.Get(item).Storage;
        if (category == EnumInventoryStorageCategory.None)
        {
            return true; //for now.
        }
        BasicList<ItemAmount> list;
        int limit;
        if (category == EnumInventoryStorageCategory.Barn)
        {
            //look at barn.
            list = GetAllBarnInventoryItems();
            limit = BarnSize;
        }
        else if (category == EnumInventoryStorageCategory.Silo)
        {
            list = GetAllSiloInventoryItems();
            limit = SiloSize;
        }
        else
        {
            throw new CustomBasicException("CanAdd Not Supported");
        }
        int currentSize = list.Sum(x => x.Amount);
        currentSize += amount;
        if (currentSize > limit)
        {
            return false;
        }
        return true;
    }
    public void Add(string item, int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        ItemAmount temp = new()
        {
            Amount = amount,
            Item = item,
        };
        InventoryAdded?.Invoke(temp);
        FinalAdd(item, amount);

    }
    private void FinalAdd(string item, int amount)
    {
        _items[item] = Get(item) + amount;
        UpdateInventory();
    }

    public void Add(ItemAmount item) => Add(item.Item, item.Amount);
    public void Add(Dictionary<string, int> rewards)
    {
        foreach (var item in rewards)
        {
            Add(item.Key, item.Value); //so everything goes in one place (because new event)
            //_items[item.Key] += item.Value;
        }
        UpdateInventory();
    }



    private async void UpdateInventory()
    {
        await persist.SaveAsync(farm, _items);
        InventoryChanged?.Invoke();
    }
    public void Consume(ItemAmount item)
    {
        Consume(item.Item, item.Amount);
    }
    public void Consume(string item, int amount)
    {
        if (Has(item, amount) == false)
        {
            throw new InvalidOperationException("Not enough items");
        }
        ItemAmount temp = new(item, amount);
        InventoryConsumed?.Invoke(temp);
        _items[item] -= amount;
        UpdateInventory();
    }
    public void Consume(Dictionary<string, int> requirements)
    {
        if (Has(requirements) == false)
        {
            throw new InvalidOperationException("Not enough items");
        }
        foreach (var req in requirements)
        {
            ItemAmount item = new(req.Key, req.Value);
            InventoryConsumed?.Invoke(item);
            _items[req.Key] -= req.Value;
        }
        UpdateInventory();
    }

    public BasicList<ItemAmount> GetAllBarnInventoryItems()
    {
        var output = GetAllInventoryItems();
        output.KeepConditionalItems(x => HasProperItem(x.Item, EnumInventoryStorageCategory.Barn));
        return output;
    }

    private bool HasProperItem(string item, EnumInventoryStorageCategory category) => itemRegistry.Get(item).Storage == category;

    public BasicList<ItemAmount> GetAllSiloInventoryItems()
    {
        var output = GetAllInventoryItems();
        output.KeepConditionalItems(x => HasProperItem(x.Item, EnumInventoryStorageCategory.Silo));
        return output;
    }


    private BasicList<ItemAmount> GetAllInventoryItems()
    {
        return _items
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => new ItemAmount(kvp.Key, kvp.Value))
            .ToBasicList();
    }
}
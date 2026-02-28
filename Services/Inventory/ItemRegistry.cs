namespace FarmSim.Domain.Services.Inventory;
public class ItemRegistry
{
    //each player will have their own registry.

    private Dictionary<string, ItemDefinition> _items = [];

    public void Register(ItemDefinition item)
    {
        _items[item.ItemName] = item;
    }

    public ItemDefinition Get(string name)
    {
        if (_items.TryGetValue(name, out var def))
        {
            return def;
        }

        // Fallback: unknown item → safe defaults
        return new ItemDefinition(
            ItemName: name,
            Storage: EnumInventoryStorageCategory.None,
            ItemCategory: EnumInventoryItemCategory.None
        );
    }

}

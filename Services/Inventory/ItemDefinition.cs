namespace FarmSim.Domain.Services.Inventory;
public readonly record struct ItemDefinition(string ItemName, EnumInventoryStorageCategory Storage, EnumInventoryItemCategory ItemCategory);
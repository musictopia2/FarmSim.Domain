namespace FarmSim.Domain.Services.Achievements;
public class AchievementProfileModel
{
    // Workshop: you wanted building + crafted item + count
    public BasicList<WorkshopQueuedProgress> WorkshopQueued { get; set; } = [];
    public BasicList<WorksiteFoundProgress> WorksiteFoundProgress { get; set; } = [];
    public BasicList<ConsumableProgress> Consumables { get; set; } = [];
    public BasicList<AnimalCollectProgress> AnimalCollectProgress { get; set; } = [];
    public BasicList<OrderItemProgress> OrderItemProgress { get; set; } = [];
    public BasicList<TimedBoostProgress> TimedBoostProgress { get; set; } = [];
    public BasicList<InstantUnlimitedProgress> InstantUnlimitedProgress { get; set; } = [];
    public BasicList<AutomationUpgradeProgress> AutomationUpgradeProgress { get; set; } = [];
    public int OrdersCompleted { get; set; }
    public int CoinsSpent { get; set; }
    public int CoinsEarned { get; set; }
    public int ChestsOpened { get; set; }

    // Spend coin: only total spent
    public long TotalCoinsSpent { get; set; }
    public int ScenariosCompleted { get; set; }
}

namespace FarmSim.Domain.Services.Achievements;
public static class AchievementCounterKeys
{
    public const string Chests = "Chests";
    public const string SpendCoin = "SpendCoin";
    public const string CoinEarned = "CoinEarned";
    public const string UseConsumable = "UseConsumable";
    public const string UseTimedBoost = "UseTimedBoost";
    public const string Level = "Level";
    public const string CollectFromAnimal = "CollectFromAnimal";
    public const string FindFromWorksites = "FindFromWorksites";
    public const string CraftFromWorkshops = "CraftFromWorkshop";
    public const string CompleteOrders = "CompleteOrders"; //ai suggested just complete orders period.
    public const string CompleteScenarios = "CompleteScenarios";
    //first needs to get the instant unlimited to work and test.
    public const string SpecificInstantUnlimited = "InstantUnlimited";
    public const string QueUpgrade = "QueUpgrade"; //for que upgrade, i like this to be the repeatable one.   figure out which ones i want to allow more in que.
    public const string ReducedTimeUpgrade = "ReducedTimeUpgrade";
    public const string VirtualCount = "VirtualCount";
}
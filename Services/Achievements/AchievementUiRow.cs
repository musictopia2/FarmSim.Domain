namespace FarmSim.Domain.Services.Achievements;
public class AchievementUiRow
{
    public string Title { get; set; } = "";
    public int Current { get; set; }
    public int Target { get; set; }          // "next target" for Current tab, "last completed tier" for Completed tab
    public int CoinReward { get; set; }      // reward at Target
    public bool IsRepeatable { get; set; }
    public string ProgressText => $"{Current:N0} / {Target:N0}";
    public double Percent => Target <= 0 ? 0 : Math.Min(1, (double)Current / Target);
}
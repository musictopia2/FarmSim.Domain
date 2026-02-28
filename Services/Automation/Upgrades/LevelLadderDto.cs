namespace FarmSim.Domain.Services.Automation.Upgrades;
public sealed class LevelLadderDto
{
    // 1-based levels: index 0 == level 1
    public BasicList<int> ValueByLevel { get; set; } = [];

    // matching length (or allow shorter + clamp last)
    public BasicList<int> CostByLevel { get; set; } = [];

    // If true and player goes past the table, keep using last entry.
    // (or you can use “growth after max” below)
    public bool ClampToLast { get; set; } = true;

    // Optional: for unlimited levels, after table ends, add this per level
    // (lets you keep FULL control early, and still allow infinite later)
    public int? ValuePerLevelAfterMax { get; set; }
    public int? CostPerLevelAfterMax { get; set; }
}
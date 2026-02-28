namespace FarmSim.Domain.Services.Progression;
public class ProgressionProfileModel
{
    // Player-facing
    public int Level { get; set; } = 1;
    // Internal counter for THIS level (player sees as progress bar value) (used so it can figure out the progress)
    public int PointsThisLevel { get; set; } = 0;
    public bool CompletedGame { get; set; } //if you are at the highest level and completed it and no more left, then you completed the game.
}
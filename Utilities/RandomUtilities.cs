using CommonBasicLibraries.AdvancedGeneralFunctionsAndProcesses.RandomGenerator;
using rs2 = CommonBasicLibraries.AdvancedGeneralFunctionsAndProcesses.RandomGenerator.RandomHelpers;

namespace FarmSim.Domain.Utilities;
public static class RandomUtilities
{
    public static readonly IRandomNumberList Randoms = rs2.GetRandomGenerator();

    public static bool RollHit(double chancePercent)
    {
        if (chancePercent == 100)
        {
            return true;
        }
        chancePercent = Math.Clamp(chancePercent, 0d, 100d);

        const int scale = 1000;
        int maxRoll = 100 * scale; // 100000

        int threshold = (int)Math.Round(chancePercent * scale, MidpointRounding.AwayFromZero);
        threshold = Math.Clamp(threshold, 0, maxRoll);

        int roll = Randoms.GetRandomNumber(maxRoll, 1); // 1..100000
        return roll <= threshold;
    }

    public static int ComputeUnlimitedBonus(int requested, double chancePercent, int penaltyDivisor = 2)
    {
        if (requested <= 0 || chancePercent <= 0)
        {
            return 0;
        }

        chancePercent = Math.Clamp(chancePercent, 0d, 100d);

        // bonus = floor(requested * chance% / 100 / penaltyDivisor)
        double raw = (requested * (chancePercent / 100d)) / penaltyDivisor;

        int bonus = (int)Math.Floor(raw);
        return Math.Max(0, bonus);
    }

}
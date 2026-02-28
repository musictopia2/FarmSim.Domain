namespace FarmSim.Domain.Services.Core;
internal static class TimingRules
{
    public static TimeSpan SaveThrottle { get; set; } = TimeSpan.FromSeconds(2);

    public static double ValidateMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            throw new CustomBasicException($"Time multiplier must be > 0 and finite. Value={value}");
        }
        return value;
    }
}

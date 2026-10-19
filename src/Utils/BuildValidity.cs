namespace AutoPowerMeter.Utils;

/// <summary>Build-relative validity; no UI, logging, or configurable runtime override.</summary>
internal static class BuildValidity
{
    internal const long ValidityMinutes = 518_400;
    internal static bool CanInitialize => IsWithinValidity(BuildValidityStamp.UtcTicks, DateTime.UtcNow.Ticks);

    internal static bool IsWithinValidity(long buildUtcTicks, long nowUtcTicks)
        => buildUtcTicks > 0 && nowUtcTicks >= buildUtcTicks
            && nowUtcTicks - buildUtcTicks <= ValidityMinutes * TimeSpan.TicksPerMinute;
}

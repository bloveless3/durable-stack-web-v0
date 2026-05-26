namespace DurableStack.Api.Services;

internal static class TelemetryLifecycleTime
{
    public static bool TryGetBucketInterval(string bucketSize, out TimeSpan interval)
    {
        var normalized = NormalizeBucketSize(bucketSize);

        switch (normalized)
        {
            case "1m":
                interval = TimeSpan.FromMinutes(1);
                return true;
            case "15m":
                interval = TimeSpan.FromMinutes(15);
                return true;
            case "2h":
                interval = TimeSpan.FromHours(2);
                return true;
            case "12h":
                interval = TimeSpan.FromHours(12);
                return true;
            default:
                interval = default;
                return false;
        }
    }

    public static DateTimeOffset AlignToBucket(DateTimeOffset valueUtc, TimeSpan interval)
    {
        var ticks = valueUtc.UtcDateTime.Ticks;
        var bucketTicks = interval.Ticks;
        var alignedTicks = ticks - (ticks % bucketTicks);
        return new DateTimeOffset(alignedTicks, TimeSpan.Zero);
    }

    public static string NormalizeBucketSize(string bucketSize)
    {
        return string.IsNullOrWhiteSpace(bucketSize)
            ? string.Empty
            : bucketSize.Trim().ToLowerInvariant();
    }
}

using System;
using System.Threading;

namespace BetOddsIngestor;

/// <summary>
/// Provides a single, cross-platform Eastern Time zone instance and helpers for converting
/// to and from UTC. Windows uses "Eastern Standard Time" while many Linux distros use
/// "America/New_York"; this resolver tries both and fails fast with a clear error message
/// if neither is available.
/// </summary>
public static class EasternTime
{
    private static readonly Lazy<TimeZoneInfo> _zone = new(
        ResolveEasternTimeZone,
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the Eastern Time zone, resolved lazily and cached for reuse.
    /// </summary>
    public static TimeZoneInfo Zone => _zone.Value;

    /// <summary>
    /// Converts a UTC <see cref="DateTime"/> to Eastern Time.
    /// </summary>
    public static DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            Zone);
    }

    /// <summary>
    /// Converts an Eastern Time <see cref="DateTime"/> (unspecified kind) to UTC.
    /// </summary>
    public static DateTime ConvertToUtc(DateTime easternDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(easternDateTime, DateTimeKind.Unspecified),
            Zone);
    }

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        var candidates = new[] { "Eastern Standard Time", "America/New_York" };

        foreach (var id in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try next candidate
            }
            catch (InvalidTimeZoneException)
            {
                // Try next candidate
            }
        }

        throw new InvalidOperationException(
            "Could not resolve the Eastern Time zone. Ensure tzdata is installed and that either 'Eastern Standard Time' or 'America/New_York' is available on this host.");
    }
}

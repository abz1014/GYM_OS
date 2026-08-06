namespace GymOS.Domain.Common;

/// <summary>
/// Which day a moment belongs to, from the point of view of the person who was there.
///
/// Everything a member cares about is counted in days: the weekly ring, the streak, whether they
/// trained today, whether a visit was ever written down. Those days were UTC days, which is nobody's
/// day. In New York a session finished at 8pm falls on the following UTC date — so an evening
/// trainer, which is most of a gym, had their work counted against tomorrow. A Tuesday session
/// showed up as Wednesday, a streak broke while the member was training daily, and two sessions
/// eleven minutes apart either side of midnight landed on different days.
///
/// The branch already stores its timezone and nothing ever read it. This is the one place that
/// turns an instant into a date, so every count that says "day" means the same day.
///
/// An unknown or unset zone falls back to UTC rather than throwing: a gym typing something wrong
/// into settings should get slightly odd day boundaries, not a member portal that will not load.
/// </summary>
public static class GymDay
{
    /// <summary>
    /// Resolves a stored timezone id, tolerating both IANA ("America/New_York") and Windows
    /// ("Eastern Standard Time") forms since .NET accepts either, and anything unrecognised.
    /// </summary>
    public static TimeZoneInfo ZoneOrUtc(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>The calendar date this instant fell on, where it happened.</summary>
    public static DateOnly Of(DateTimeOffset instant, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>The local wall-clock time of an instant — for saying "you checked in at 6:42pm".</summary>
    public static DateTimeOffset In(DateTimeOffset instant, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(instant, zone);
}

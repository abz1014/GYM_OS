using GymOS.Domain.Common;
using Shouldly;

namespace GymOS.Domain.Tests.Common;

/// <summary>
/// Days, from where the member is standing. The scenario every test here defends is the ordinary
/// one: someone trains in the evening, and the app has to agree with them about what day it was.
/// </summary>
public class GymDayTests
{
    private static readonly TimeZoneInfo NewYork = GymDay.ZoneOrUtc("America/New_York");

    [Fact]
    public void An_evening_session_belongs_to_the_evening_it_happened()
    {
        // 8pm Thursday in New York is already Friday in UTC. It is still Thursday's session.
        var eightPmThursday = new DateTimeOffset(2026, 8, 6, 20, 0, 0, TimeSpan.FromHours(-4));

        GymDay.Of(eightPmThursday, NewYork).ShouldBe(new DateOnly(2026, 8, 6));
        GymDay.Of(eightPmThursday, TimeZoneInfo.Utc).ShouldBe(new DateOnly(2026, 8, 7)); // the old answer
    }

    [Fact]
    public void Two_sessions_either_side_of_local_midnight_are_two_days()
    {
        var lateTuesday = new DateTimeOffset(2026, 8, 4, 23, 58, 0, TimeSpan.FromHours(-4));
        var earlyWednesday = new DateTimeOffset(2026, 8, 5, 0, 7, 0, TimeSpan.FromHours(-4));

        GymDay.Of(lateTuesday, NewYork).ShouldBe(new DateOnly(2026, 8, 4));
        GymDay.Of(earlyWednesday, NewYork).ShouldBe(new DateOnly(2026, 8, 5));
    }

    [Fact]
    public void A_whole_local_day_stays_on_that_day_from_first_minute_to_last()
    {
        var day = new DateOnly(2026, 8, 6);
        for (var hour = 0; hour < 24; hour++)
        {
            var instant = new DateTimeOffset(2026, 8, 6, hour, 30, 0, TimeSpan.FromHours(-4));
            GymDay.Of(instant, NewYork).ShouldBe(day, $"failed at {hour}:30 local");
        }
    }

    [Fact]
    public void The_same_instant_is_a_different_day_in_two_gyms()
    {
        // A franchise spanning timezones must not force one gym onto the other's calendar.
        var instant = new DateTimeOffset(2026, 8, 7, 2, 0, 0, TimeSpan.Zero);

        GymDay.Of(instant, NewYork).ShouldBe(new DateOnly(2026, 8, 6));
        GymDay.Of(instant, GymDay.ZoneOrUtc("Asia/Karachi")).ShouldBe(new DateOnly(2026, 8, 7));
    }

    [Fact]
    public void Iana_and_windows_spellings_both_resolve_to_the_same_zone()
    {
        // Gyms are configured by people, and .NET accepts either form.
        GymDay.ZoneOrUtc("America/New_York").BaseUtcOffset.ShouldBe(GymDay.ZoneOrUtc("Eastern Standard Time").BaseUtcOffset);
    }

    [Fact]
    public void A_zone_nobody_recognises_falls_back_to_utc_rather_than_breaking_the_app()
    {
        GymDay.ZoneOrUtc("Mars/Olympus_Mons").ShouldBe(TimeZoneInfo.Utc);
        GymDay.ZoneOrUtc("").ShouldBe(TimeZoneInfo.Utc);
        GymDay.ZoneOrUtc(null).ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void Daylight_saving_does_not_shift_the_day()
    {
        // US clocks go forward on 8 March 2026. The day either side is still the day.
        var beforeDst = new DateTimeOffset(2026, 3, 7, 22, 0, 0, TimeSpan.FromHours(-5));
        var afterDst = new DateTimeOffset(2026, 3, 8, 22, 0, 0, TimeSpan.FromHours(-4));

        GymDay.Of(beforeDst, NewYork).ShouldBe(new DateOnly(2026, 3, 7));
        GymDay.Of(afterDst, NewYork).ShouldBe(new DateOnly(2026, 3, 8));
    }

    [Fact]
    public void Local_wall_clock_is_available_for_telling_a_member_when_they_arrived()
    {
        var instant = new DateTimeOffset(2026, 8, 6, 22, 42, 0, TimeSpan.Zero);

        GymDay.In(instant, NewYork).Hour.ShouldBe(18); // 6:42pm where they were
    }
}

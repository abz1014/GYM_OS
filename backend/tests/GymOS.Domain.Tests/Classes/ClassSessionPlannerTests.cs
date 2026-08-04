using GymOS.Domain.Classes;
using Shouldly;

namespace GymOS.Domain.Tests.Classes;

/// <summary>
/// The recurrence rule that turns a weekly ClassSchedule into concrete dated sessions is the core
/// of the whole Classes/booking feature — it decides exactly which days become bookable. Tested
/// directly (pure, no DB) the same way MaintenanceSchedule's due-date logic is domain logic.
/// </summary>
public class ClassSessionPlannerTests
{
    private static ClassSchedule MondaySpin() => new()
    {
        TenantId = Guid.NewGuid(),
        BranchId = Guid.NewGuid(),
        ClassTypeId = Guid.NewGuid(),
        DayOfWeek = DayOfWeek.Monday,
        StartTime = new TimeOnly(18, 0),
        DurationMinutes = 45,
        Capacity = 20,
        Location = "Studio A"
    };

    [Fact]
    public void Generates_one_session_per_matching_weekday_in_the_window()
    {
        var schedule = MondaySpin();
        // 2026-08-03 is a Monday; an inclusive window through the next Monday (08-10) covers exactly
        // two Mondays (03, 10).
        var from = new DateOnly(2026, 8, 3);
        var through = from.AddDays(7);

        var sessions = ClassSessionPlanner.BuildSessions(schedule, from, through, new HashSet<DateOnly>());

        sessions.Count.ShouldBe(2);
        sessions.ShouldAllBe(s => s.StartsAt.DayOfWeek == DayOfWeek.Monday);
        sessions.ShouldAllBe(s => s.StartsAt.TimeOfDay == new TimeOnly(18, 0).ToTimeSpan());
    }

    [Fact]
    public void Copies_the_schedules_prescription_onto_each_session()
    {
        var schedule = MondaySpin();
        var from = new DateOnly(2026, 8, 3);

        var session = ClassSessionPlanner.BuildSessions(schedule, from, from, new HashSet<DateOnly>()).ShouldHaveSingleItem();

        session.ClassScheduleId.ShouldBe(schedule.Id);
        session.ClassTypeId.ShouldBe(schedule.ClassTypeId);
        session.Capacity.ShouldBe(20);
        session.DurationMinutes.ShouldBe(45);
        session.Location.ShouldBe("Studio A");
        session.Status.ShouldBe(ClassSessionStatus.Scheduled);
    }

    [Fact]
    public void Skips_dates_that_already_have_a_session_so_regeneration_never_double_books()
    {
        var schedule = MondaySpin();
        var from = new DateOnly(2026, 8, 3);
        var through = from.AddDays(7);
        var alreadyGenerated = new HashSet<DateOnly> { new(2026, 8, 3) };

        var sessions = ClassSessionPlanner.BuildSessions(schedule, from, through, alreadyGenerated);

        sessions.ShouldHaveSingleItem();
        sessions[0].StartsAt.Date.ShouldBe(new DateTime(2026, 8, 10));
    }

    [Fact]
    public void Produces_nothing_when_no_day_in_the_window_matches()
    {
        var schedule = MondaySpin();
        // Tue 2026-08-04 through Sun 2026-08-09 contains no Monday.
        var sessions = ClassSessionPlanner.BuildSessions(
            schedule, new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 9), new HashSet<DateOnly>());

        sessions.ShouldBeEmpty();
    }
}

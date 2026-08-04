namespace GymOS.Domain.Classes;

/// <summary>
/// Turns a recurring ClassSchedule into concrete, dated ClassSession rows over a date window.
/// Deliberately pure (no DB, no clock) so both the background generation job and the demo seeder
/// share one definition of "what sessions should exist," and so the recurrence logic is unit
/// testable on its own — mirroring how MaintenanceSchedule's next-due advancement is domain logic,
/// not job logic.
/// </summary>
public static class ClassSessionPlanner
{
    /// <summary>How many days ahead concrete sessions are materialised — the rolling booking
    /// window. Shared by the create-schedule command (immediate first fill), the daily generation
    /// job (keeps the window topped up), and the demo seeder.</summary>
    public const int DefaultWindowDays = 14;

    /// <summary>
    /// Every ClassSession that should exist for <paramref name="schedule"/> on the days in
    /// [<paramref name="fromDate"/>, <paramref name="throughDate"/>] matching its DayOfWeek, minus
    /// any date already present in <paramref name="existingDates"/> (idempotency — a session is
    /// keyed by its calendar date, so a re-run never double-books a day).
    /// </summary>
    public static List<ClassSession> BuildSessions(
        ClassSchedule schedule, DateOnly fromDate, DateOnly throughDate, ISet<DateOnly> existingDates)
    {
        var sessions = new List<ClassSession>();

        for (var date = fromDate; date <= throughDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != schedule.DayOfWeek || existingDates.Contains(date))
            {
                continue;
            }

            sessions.Add(new ClassSession
            {
                TenantId = schedule.TenantId,
                BranchId = schedule.BranchId,
                ClassScheduleId = schedule.Id,
                ClassTypeId = schedule.ClassTypeId,
                TrainerId = schedule.TrainerId,
                StartsAt = new DateTimeOffset(date.ToDateTime(schedule.StartTime), TimeSpan.Zero),
                DurationMinutes = schedule.DurationMinutes,
                Capacity = schedule.Capacity,
                Location = schedule.Location,
                Status = ClassSessionStatus.Scheduled
            });
        }

        return sessions;
    }
}

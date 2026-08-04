using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Classes;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Keeps the rolling booking window full: every active ClassSchedule should have concrete
/// ClassSession rows materialised out to today + DefaultWindowDays. Registered daily via Hangfire.
/// Runs across every tenant with IgnoreQueryFilters() (no ambient tenant/user in a background job),
/// and delegates the actual date maths to the pure ClassSessionPlanner so the "what should exist"
/// rule is identical to the create-schedule command and the demo seeder.
/// </summary>
public class ClassSessionGenerationJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<ClassSessionGenerationJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var throughDate = today.AddDays(ClassSessionPlanner.DefaultWindowDays);

        var schedules = await db.ClassSchedules.IgnoreQueryFilters()
            .Where(s => s.IsActive && (s.GeneratedThroughDate == null || s.GeneratedThroughDate < throughDate))
            .ToListAsync(cancellationToken);

        var totalCreated = 0;

        foreach (var schedule in schedules)
        {
            // Only look at dates we haven't already covered — GeneratedThroughDate is the resume
            // point, so a daily run typically just materialises the single newly-entered day.
            var from = schedule.GeneratedThroughDate is { } g && g >= today ? g.AddDays(1) : today;

            var existingDates = await db.ClassSessions.IgnoreQueryFilters()
                .Where(s => s.ClassScheduleId == schedule.Id && s.Status != ClassSessionStatus.Cancelled)
                .Select(s => DateOnly.FromDateTime(s.StartsAt.UtcDateTime))
                .ToListAsync(cancellationToken);

            var sessions = ClassSessionPlanner.BuildSessions(schedule, from, throughDate, existingDates.ToHashSet());
            db.ClassSessions.AddRange(sessions);
            schedule.GeneratedThroughDate = throughDate;
            totalCreated += sessions.Count;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Class session generation created {Count} session(s) across {ScheduleCount} schedule(s)", totalCreated, schedules.Count);
        return totalCreated;
    }
}

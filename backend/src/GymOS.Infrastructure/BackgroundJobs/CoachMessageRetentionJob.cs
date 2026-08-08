using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Removes coach↔member correspondence once it is past <see cref="CoachMessagePolicy.RetentionPeriod"/>.
///
/// This is the first thing in GymOS that deletes anything on a schedule, and it starts here rather
/// than anywhere else on purpose: these rows are the only place in the product where two people
/// write free text to each other about somebody's body, injuries and health. Attendance and invoices
/// are records a gym is expected to keep; a conversation about a sore shoulder is not, and keeping it
/// forever is a decision nobody made.
///
/// It runs across every tenant with filters ignored, the way the other recurring jobs do — there is
/// no ambient user on a background thread to scope by, and a retention rule that only applied to
/// whichever tenant happened to be in context would be worse than none.
///
/// Deliberately a hard delete rather than a soft flag. A retention policy whose rows are still there
/// is not a retention policy, and a `DeletedAt` column would leave the text exactly where it was
/// while letting everyone believe otherwise.
/// </summary>
public class CoachMessageRetentionJob(
    GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<CoachMessageRetentionJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        // Pulled to memory before the age compare: a DateTimeOffset range filter does not translate
        // on SQLite, which is the test provider, and the same workaround the rest of this codebase
        // uses over these tables. The set is bounded by the retention period, not by table size.
        var expired = (await db.CoachMessages.IgnoreQueryFilters()
                .Select(c => new { c.Id, c.SentAt })
                .ToListAsync(cancellationToken))
            .Where(c => CoachMessagePolicy.IsExpired(c.SentAt, now))
            .Select(c => c.Id)
            .ToList();

        if (expired.Count == 0)
        {
            return 0;
        }

        await db.CoachMessages.IgnoreQueryFilters()
            .Where(c => expired.Contains(c.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Logged rather than silent. Something that deletes member correspondence should say how much
        // it took every time it runs, so the day the number looks wrong there is a record of it.
        logger.LogInformation(
            "Coach message retention removed {Count} message(s) older than {Days} days",
            expired.Count,
            (int)CoachMessagePolicy.RetentionPeriod.TotalDays);

        return expired.Count;
    }
}

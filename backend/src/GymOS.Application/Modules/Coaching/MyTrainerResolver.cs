using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Domain.Trainers;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Coaching;

/// <summary>
/// Turns "the authenticated staff member" into "their own Trainer row", the mirror of
/// MyMemberResolver on the coaching side. A trainer id is never accepted from the caller, so
/// "message my client" cannot become "message anybody's client".
/// </summary>
internal static class MyTrainerResolver
{
    public static async Task<Guid> ResolveTrainerIdAsync(
        IApplicationDbContext db, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenAccessException("No authenticated user.");
        }

        var trainerId = await db.Trainers.AsNoTracking()
            .Where(t => t.UserId == currentUser.UserId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return trainerId ?? throw new ForbiddenAccessException("This account is not a trainer.");
    }

    /// <summary>
    /// The pairing between this trainer and this member, or null if there has never been one.
    /// Returned rather than a bool because an ended pairing still permits reading the history, and
    /// only the dates can tell the two apart.
    /// </summary>
    public static async Task<(DateOnly Start, DateOnly? End, bool IsActive)?> FindPairingAsync(
        IApplicationDbContext db, Guid trainerId, Guid memberId, CancellationToken cancellationToken)
    {
        var pairings = await db.TrainerAssignments.AsNoTracking()
            .Where(a => a.TrainerId == trainerId && a.MemberId == memberId)
            .Select(a => new { a.StartDate, a.EndDate, a.IsActive })
            .ToListAsync(cancellationToken);

        var latest = pairings.OrderByDescending(a => a.StartDate).FirstOrDefault();
        return latest is null ? null : (latest.StartDate, latest.EndDate, latest.IsActive);
    }

    /// <summary>The gym clock this pairing is judged on, so "today" means the same on both sides.</summary>
    public static async Task<TimeZoneInfo> ResolveGymZoneAsync(
        IApplicationDbContext db, Guid trainerId, CancellationToken cancellationToken)
    {
        var timeZoneId = await (from t in db.Trainers.AsNoTracking()
                                join b in db.Branches.AsNoTracking() on t.BranchId equals b.Id
                                where t.Id == trainerId
                                select b.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

        return GymDay.ZoneOrUtc(timeZoneId);
    }
}

using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal;

/// <summary>
/// The single place that turns "the authenticated caller" into "their own Member row" via
/// Member.UserId — never a client-supplied id. Every Portal query goes through this so a member
/// can only ever resolve to their own data; there is no parameter here for anyone to tamper with.
/// This is the fix for the cross-member data exposure: the staff-facing Get*ByMemberId queries
/// trusted a caller-supplied memberId, which is correct for staff (permission-gated) but was never
/// safe to also hand to the Member role, since it let one member read another's records.
/// </summary>
internal static class MyMemberResolver
{
    public static async Task<Guid> ResolveMemberIdAsync(IApplicationDbContext db, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenAccessException("No authenticated user.");
        }

        var memberId = await db.Members.AsNoTracking()
            .Where(m => m.UserId == currentUser.UserId)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return memberId ?? throw new NotFoundException("MemberProfile", currentUser.UserId);
    }

    /// <summary>
    /// The timezone of the gym this member belongs to — the clock that decides what "today" means
    /// for them. Everything a member counts is counted in days, and a UTC day is nobody's day: in
    /// New York an 8pm session lands on the next UTC date, so evening training was being counted
    /// against the following day. Resolved per request rather than cached because a member moves
    /// branch rarely and correctness here is worth one small read.
    /// </summary>
    public static async Task<TimeZoneInfo> ResolveGymZoneAsync(
        IApplicationDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        // Member carries a BranchId but no navigation to it, so this joins the tenant-scoped branch
        // table directly — the same shape the portal queries use for the exercise catalogue.
        var timeZoneId = await (from m in db.Members.AsNoTracking()
                                join b in db.Branches.AsNoTracking() on m.BranchId equals b.Id
                                where m.Id == memberId
                                select b.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

        return GymDay.ZoneOrUtc(timeZoneId);
    }
}

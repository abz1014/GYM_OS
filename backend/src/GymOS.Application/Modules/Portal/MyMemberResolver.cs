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

    /// <summary>
    /// The gym's timezone as the gym CONFIGURED it — the raw IANA id off Branch.TimeZone, not
    /// TimeZoneInfo.Id.
    ///
    /// The distinction is load-bearing for anything sent to a browser. .NET normalises an id to the
    /// host platform's naming, so the same branch yields "America/New_York" on the Linux container
    /// and "Eastern Standard Time" on a Windows dev machine — and Intl.DateTimeFormat only
    /// understands the first. Returning the stored string keeps the value the same wherever the API
    /// happens to be running.
    /// </summary>
    public static async Task<string?> ResolveGymZoneIdAsync(
        IApplicationDbContext db, Guid memberId, CancellationToken cancellationToken)
        => await (from m in db.Members.AsNoTracking()
                  join b in db.Branches.AsNoTracking() on m.BranchId equals b.Id
                  where m.Id == memberId
                  select b.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);
}

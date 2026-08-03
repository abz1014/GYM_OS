using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
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
}

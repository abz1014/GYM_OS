using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal;

/// <summary>
/// Turns "this member" into "the ONE membership the portal means when it says my membership".
///
/// A member accumulates memberships — last year's expired annual, this year's active one, a
/// pending renewal that starts next month — and every self-service action (freeze, resume,
/// auto-renew, cancel request) has to pick exactly one of them without the caller naming it, since
/// naming it would be a client-supplied id and the whole portal exists to not have those.
///
/// The ordering is lifted verbatim from GetMemberByIdQuery: live plan first (Active, then Frozen,
/// then PendingActivation, then everything finished), latest EndDate as the tiebreak. That is not a
/// stylistic echo — the member's profile screen already shows "your membership" using that order, so
/// any other rule here would let the freeze button act on a different membership from the one the
/// member was looking at when they pressed it.
/// </summary>
internal static class MyMembershipResolver
{
    public static async Task<Guid> ResolveCurrentMembershipIdAsync(
        IApplicationDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        // Projected and reduced in memory rather than ordered in SQL: the CASE-over-status ordering
        // is the same shape GetMemberByIdQuery applies to an already-materialised list, and a member
        // has a handful of memberships, not a page of them.
        var memberships = await db.MemberMemberships.AsNoTracking()
            .Where(mm => mm.MemberId == memberId)
            .Select(mm => new { mm.Id, mm.Status, mm.EndDate })
            .ToListAsync(cancellationToken);

        var current = memberships
            .OrderBy(mm => mm.Status switch
            {
                MemberMembershipStatus.Active => 0,
                MemberMembershipStatus.Frozen => 1,
                MemberMembershipStatus.PendingActivation => 2,
                _ => 3,
            })
            .ThenByDescending(mm => mm.EndDate)
            .FirstOrDefault();

        /*
         * A member with no membership row at all is a genuine not-found, not a validation error: the
         * thing they are asking to freeze/resume/renew does not exist. Note this deliberately does
         * NOT pre-judge state — an expired or cancelled membership still resolves, and is then
         * refused by the staff command's own entry-state guard with the sentence that explains why
         * ("Only an active membership can be frozen; this one is Expired"). Filtering it out here
         * would replace that sentence with a bare 404 and leave the member guessing.
         */
        return current?.Id ?? throw new NotFoundException(nameof(MemberMembership), memberId);
    }
}

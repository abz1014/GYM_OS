using GymOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Common.Security;

/// <summary>
/// The single definition of "which branches can this user touch": exactly whatever rows exist
/// in UserBranchAccess for them. Owner/Manager get every branch there at seed/account-creation
/// time, so there is no special-casing by role here — access is purely data-driven, which is
/// what makes it safe to reuse identically for every role including Owner.
///
/// Found via a live security review: query handlers across Attendance/CRM/Dashboard/Equipment/
/// Maintenance/Members/Trainers accepted an optional BranchId filter and, when it was omitted,
/// returned every branch's data with no server-side restriction — UserBranchAccess was populated
/// at login only to drive the frontend's branch-switcher dropdown, never actually enforced.
/// Confirmed live: a Receptionist seeded with access to exactly one branch could read another
/// branch's full member list by passing its id directly, or every branch's members by omitting
/// the filter entirely.
/// </summary>
public static class BranchAccessResolver
{
    public static async Task<List<Guid>> GetAccessibleBranchIdsAsync(
        IApplicationDbContext db, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return [];
        }

        return await db.UserBranchAccesses.AsNoTracking()
            .Where(a => a.UserId == currentUser.UserId)
            .Select(a => a.BranchId)
            .ToListAsync(cancellationToken);
    }
}

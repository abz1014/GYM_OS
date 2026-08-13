using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Common.Security;

/// <summary>
/// Proves a member is visible to the caller before anything belonging to that member is read.
///
/// WHY THIS EXISTS. Tenant and branch isolation is enforced by EF Core global query filters, and
/// those filters attach to an entity only if the entity carries the column they filter on. Member is
/// <c>IBranchScoped</c>, so <c>db.Members</c> is already narrowed to the branches the caller can
/// actually see. WorkoutLog, WorkoutAssignment, DietPlan and WaterLog are tenant-scoped but carry NO
/// BranchId — nothing for a branch filter to attach to — so a query written as
/// <c>db.WorkoutLogs.Where(l =&gt; l.MemberId == request.MemberId)</c> is filtered by the caller's own
/// route parameter and by nothing else.
///
/// The result was a live cross-branch leak, reproduced before this was written: a trainer with access
/// to Downtown only asked for an Uptown member and got
/// <c>GET /api/members/{id}</c> =&gt; 404 — correctly invisible — while
/// <c>GET /api/workouts/logs/member/{id}</c> =&gt; 200 with seven complete sessions, exercises, sets,
/// reps and loads. The member was hidden; their training history was not.
///
/// THE FIX IS TO ASK THE FILTERED SET. Routing every such read through <c>db.Members</c> borrows the
/// isolation that table already has, rather than reimplementing branch logic per handler — which is
/// what let four handlers drift apart in the first place. It is deliberately the same shape the
/// coaching module already used to refuse a member who is not your client.
///
/// NOT FOUND, NOT FORBIDDEN. An invisible member must be indistinguishable from a member who does not
/// exist. Answering "forbidden" would confirm the id is real, turning the endpoint into an oracle for
/// enumerating another branch's membership — and 404 is exactly what <c>GET /api/members/{id}</c>
/// already returns for the same id, so the two now agree.
/// </summary>
public static class MemberScope
{
    /// <summary>
    /// Throws <see cref="NotFoundException"/> unless the member is visible to the caller under the
    /// active tenant and branch filters. Call this BEFORE reading anything keyed on the member id.
    /// </summary>
    public static async Task EnsureVisibleAsync(
        IApplicationDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        // AnyAsync against the filtered set: the global filters do the work, and no member data is
        // materialised just to prove the caller is allowed to see it.
        var visible = await db.Members.AsNoTracking()
            .AnyAsync(m => m.Id == memberId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), memberId);
        }
    }
}

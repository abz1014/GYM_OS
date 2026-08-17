using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Billing.Dtos;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Queries;

/// <summary>
/// The failed-auto-renewal list — who declined, why, how much rope is left, and who has already
/// lost access.
///
/// RecurringBillingJob has always written a complete dunning record, and nothing in the product
/// could read a line of it: outside the job itself the only reference to
/// <see cref="RecurringBillingAttempt"/> in the whole Application layer was the DbSet declaration.
/// An owner could see that revenue was short at month-end and had no way to learn who to ring —
/// the gateway's decline reason, the retry count and the resulting suspension all sat in a table
/// with no reader. This query is that reader.
///
/// Succeeded attempts are excluded because they are not a chase: the card worked, the membership
/// renewed, and leaving them in would bury the handful of rows that need a phone call under every
/// renewal the gym has ever collected. Cancelled (staff intervened) is kept — it is a real outcome
/// someone may need to see, and it is rare.
/// </summary>
public record GetDunningAttemptsQuery : IQuery<List<DunningAttemptDto>>;

public class GetDunningAttemptsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDunningAttemptsQuery, List<DunningAttemptDto>>
{
    public Task<List<DunningAttemptDto>> Handle(GetDunningAttemptsQuery request, CancellationToken cancellationToken)
    {
        // Tenant and branch scoping come from the global query filters — RecurringBillingAttempt is
        // IBranchScoped, so a manager with one branch sees one branch's chase list without this
        // handler having to remember to say so.
        return db.RecurringBillingAttempts.AsNoTracking()
            .Where(a => a.Status != RecurringBillingStatus.Succeeded)
            // NextAttemptDate is a DateOnly, so this ordering is safe to translate; the SQLite
            // restriction that pushes DateTimeOffset ordering into memory elsewhere does not apply.
            .OrderBy(a => a.NextAttemptDate)
            .Select(a => new DunningAttemptDto(
                a.Id,
                a.MemberId,
                a.Member!.FirstName + " " + a.Member.LastName,
                a.InvoiceId,
                a.Invoice!.InvoiceNumber,
                a.Amount,
                a.Currency,
                a.FailedAttempts,
                BillingRetryPolicy.MaxAttempts,
                a.LastFailureReason,
                a.NextAttemptDate,
                a.LastAttemptDate,
                a.Status,
                /*
                 * "Locked out today", read from the two facts that together mean it — not from a
                 * flag, because there isn't one.
                 *
                 * ApplyFailureAsync suspends by setting the membership to Frozen once the retries
                 * are exhausted, and deliberately writes no freeze window (that is what keeps a
                 * dunning suspension distinguishable from a member-requested pause, and what lets a
                 * later payment return it to Active). So the state the job leaves behind is exactly
                 * Abandoned + Frozen, and reading only the attempt status would claim a member was
                 * suspended after staff had already reinstated them.
                 */
                a.Status == RecurringBillingStatus.Abandoned
                    && a.MemberMembership!.Status == MemberMembershipStatus.Frozen))
            .ToListAsync(cancellationToken);
    }
}

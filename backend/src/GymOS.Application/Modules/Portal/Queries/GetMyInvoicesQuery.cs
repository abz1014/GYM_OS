using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member's own billing history, newest first.
///
/// Takes no member id — same rule as the rest of /api/me. That matters more here than almost
/// anywhere else in the portal: the staff-facing GetInvoicesQuery accepts a memberId filter, and an
/// invoice carries what someone pays and what they still owe. Handing the member role a query with
/// that parameter would have let any member read any other member's finances by editing a URL.
/// </summary>
public record GetMyInvoicesQuery : IQuery<List<MyInvoiceDto>>;

public class GetMyInvoicesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyInvoicesQuery, List<MyInvoiceDto>>
{
    public async Task<List<MyInvoiceDto>> Handle(GetMyInvoicesQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        // IssueDate is a DateOnly, so this ordering is safe to translate — the SQLite restriction
        // this codebase works around applies to DateTimeOffset, not to dates. InvoiceNumber breaks
        // the tie so two invoices raised on the same day never swap places between two reads.
        return await db.Invoices.AsNoTracking()
            .Where(i => i.MemberId == memberId)
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .Select(i => new MyInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.DueDate,
                i.Status,
                i.TotalAmount,
                /*
                 * Completed payments MINUS completed refunds — the same arithmetic the staff-facing
                 * GetInvoicesQuery and GetInvoiceByIdQuery use, and deliberately not a simpler sum.
                 *
                 * Only Completed payments count in the first place: a Pending or Failed attempt is
                 * not money the gym has, and counting it would tell a member their bill is settled
                 * while the dunning job is still chasing it.
                 *
                 * Subtracting refunds is what keeps this figure the SAME NUMBER staff are looking
                 * at. Without it, a refunded invoice reads as fully paid to the member and partly
                 * paid to the receptionist they are on the phone to — and a list/detail version of
                 * exactly that disagreement is already recorded as a real defect in the staff query.
                 * One invoice, one paid figure, whoever is reading it.
                 */
                i.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                    - i.Payments.SelectMany(p => p.Refunds)
                        .Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount),
                i.Currency))
            .ToListAsync(cancellationToken);
    }
}

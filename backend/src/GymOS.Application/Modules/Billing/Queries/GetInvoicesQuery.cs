using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Billing.Dtos;
using GymOS.Domain.Billing;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Queries;

public record GetInvoicesQuery(Guid? MemberId, InvoiceStatus? Status, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<InvoiceListItemDto>>;

public class GetInvoicesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInvoicesQuery, PagedList<InvoiceListItemDto>>
{
    public Task<PagedList<InvoiceListItemDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();

        if (request.MemberId is not null)
        {
            query = query.Where(i => i.MemberId == request.MemberId);
        }

        if (request.Status is not null)
        {
            query = query.Where(i => i.Status == request.Status);
        }

        var projected = query
            .OrderByDescending(i => i.IssueDate)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.MemberId, MemberName = i.Member!.FirstName + " " + i.Member.LastName,
                i.IssueDate, i.DueDate, i.Status, i.TotalAmount, i.Currency,
                /*
                 * Net of refunds, matching GetInvoiceByIdQuery.
                 *
                 * The list used to sum completed payments and stop there while the detail subtracted
                 * refunds, so the same invoice reported different money depending on which screen you
                 * were looking at — $54.99 outstanding in the list you click FROM, $64.99 in the page
                 * you land ON. The dashboard's "overdue · $X outstanding" tile sums this list, so it
                 * under-reported the debt on every invoice that had ever been refunded.
                 */
                AmountPaid = i.Payments.Where(p => p.Status == Domain.Billing.PaymentStatus.Completed).Sum(p => p.Amount)
                    - i.Payments.SelectMany(p => p.Refunds)
                        .Where(r => r.Status == Domain.Billing.RefundStatus.Completed).Sum(r => r.Amount)
            })
            .Select(x => new InvoiceListItemDto(
                x.Id, x.InvoiceNumber, x.MemberId, x.MemberName, x.IssueDate, x.DueDate, x.Status,
                x.TotalAmount, x.AmountPaid, x.TotalAmount - x.AmountPaid, x.Currency));

        return projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}

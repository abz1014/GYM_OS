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
                AmountPaid = i.Payments.Where(p => p.Status == Domain.Billing.PaymentStatus.Completed).Sum(p => p.Amount)
            })
            .Select(x => new InvoiceListItemDto(
                x.Id, x.InvoiceNumber, x.MemberId, x.MemberName, x.IssueDate, x.DueDate, x.Status,
                x.TotalAmount, x.AmountPaid, x.TotalAmount - x.AmountPaid, x.Currency));

        return projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}

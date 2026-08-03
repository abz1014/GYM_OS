using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

public record CreateInvoiceLineInput(InvoiceLineItemType ItemType, string Description, int Quantity, decimal UnitPrice);

public record CreateInvoiceCommand(
    Guid MemberId,
    Guid BranchId,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TaxAmount,
    decimal DiscountAmount,
    string Currency,
    string? Notes,
    IReadOnlyList<CreateInvoiceLineInput> Lines) : ICommand<Guid>;

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public class CreateInvoiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var sequence = await db.Invoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == tenantId, cancellationToken) + 1;
        var subtotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var total = Math.Max(0, subtotal + request.TaxAmount - request.DiscountAmount);

        var invoice = new Invoice
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            MemberId = request.MemberId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyy}-{sequence:D6}",
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            Status = InvoiceStatus.Issued,
            Subtotal = subtotal,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            TotalAmount = total,
            Currency = request.Currency,
            Notes = request.Notes
        };

        foreach (var line in request.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                ItemType = line.ItemType,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}

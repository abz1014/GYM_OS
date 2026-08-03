using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Inventory.Commands;
using GymOS.Domain.Billing;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

public record CreateInvoiceLineInput(
    InvoiceLineItemType ItemType, string Description, int Quantity, decimal UnitPrice, Guid? InventoryItemId = null);

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
            line.RuleFor(l => l.InventoryItemId).Empty()
                .When(l => l.ItemType != InvoiceLineItemType.ProductSale)
                .WithMessage("Only ProductSale lines may reference an inventory item.");
        });
    }
}

public class CreateInvoiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
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
                InventoryItemId = line.InventoryItemId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        // Closes the POS loop: a ProductSale line that names an inventory item decrements stock
        // through the same command RecordPurchaseCommand uses to add it, so there's one source of
        // truth for QuantityOnHand/StockMovement bookkeeping regardless of which direction moved
        // it. Runs after the invoice is saved so InvoiceNumber is available for the movement's
        // reason, and still inside the ambient transaction — a stock shortfall rolls the whole
        // invoice back rather than leaving an invoice with lines nobody can fulfill.
        foreach (var line in request.Lines.Where(l => l.InventoryItemId is not null))
        {
            await sender.Send(
                new RecordStockMovementCommand(
                    line.InventoryItemId!.Value, StockMovementType.Out, line.Quantity, $"Sale ({invoice.InvoiceNumber})"),
                cancellationToken);
        }

        return invoice.Id;
    }
}

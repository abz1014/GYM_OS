using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Commands;

public record RecordPurchaseCommand(
    Guid InventoryItemId, Guid? SupplierId, int Quantity, decimal UnitCost, string? InvoiceReference) : ICommand<Guid>;

public class RecordPurchaseCommandValidator : AbstractValidator<RecordPurchaseCommand>
{
    public RecordPurchaseCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public class RecordPurchaseCommandHandler(IApplicationDbContext db, ISender sender, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecordPurchaseCommand, Guid>
{
    public async Task<Guid> Handle(RecordPurchaseCommand request, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.InventoryItemId);

        // Reuse the stock-movement command for the actual QuantityOnHand/StockMovement bookkeeping
        // so there's a single source of truth for "how stock gets adjusted" (participates in the
        // same DB transaction — TransactionBehavior short-circuits for nested commands).
        await sender.Send(
            new RecordStockMovementCommand(
                item.Id,
                StockMovementType.In,
                request.Quantity,
                "Purchase" + (request.InvoiceReference is not null ? $" ({request.InvoiceReference})" : string.Empty)),
            cancellationToken);

        item.UnitCost = request.UnitCost;

        var purchase = new PurchaseRecord
        {
            InventoryItemId = item.Id,
            SupplierId = request.SupplierId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            PurchasedAt = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime),
            InvoiceReference = request.InvoiceReference
        };

        db.PurchaseRecords.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}

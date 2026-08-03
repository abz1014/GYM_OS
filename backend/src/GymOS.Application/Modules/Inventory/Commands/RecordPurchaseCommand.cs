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

public class RecordPurchaseCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecordPurchaseCommand, Guid>
{
    public async Task<Guid> Handle(RecordPurchaseCommand request, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.InventoryItemId);

        item.QuantityOnHand += request.Quantity;
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

        db.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            Type = StockMovementType.In,
            Quantity = request.Quantity,
            Reason = "Purchase" + (request.InvoiceReference is not null ? $" ({request.InvoiceReference})" : string.Empty),
            MovedAt = dateTimeProvider.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}

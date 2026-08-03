using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Inventory;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Commands;

public record RecordStockMovementCommand(Guid InventoryItemId, StockMovementType Type, int Quantity, string? Reason) : ICommand<Guid>;

public class RecordStockMovementCommandValidator : AbstractValidator<RecordStockMovementCommand>
{
    public RecordStockMovementCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public class RecordStockMovementCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecordStockMovementCommand, Guid>
{
    public async Task<Guid> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.InventoryItemId);

        if (request.Type == StockMovementType.Out && request.Quantity > item.QuantityOnHand)
        {
            throw new ValidationException($"Cannot remove {request.Quantity} units — only {item.QuantityOnHand} on hand.");
        }

        item.QuantityOnHand += request.Type == StockMovementType.In ? request.Quantity : -request.Quantity;

        var movement = new StockMovement
        {
            InventoryItemId = item.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            Reason = request.Reason,
            MovedAt = dateTimeProvider.UtcNow,
            RecordedByUserId = currentUser.UserId
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}

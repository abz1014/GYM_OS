using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Commands;

public record CreateInventoryItemCommand(
    string Sku, string Name, InventoryCategory Category, Guid BranchId, int QuantityOnHand, int ReorderLevel,
    decimal? UnitCost, decimal? UnitPrice) : ICommand<Guid>;

public class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.QuantityOnHand).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public class CreateInventoryItemCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var skuTaken = await db.InventoryItems.AnyAsync(i => i.Sku == request.Sku, cancellationToken);
        if (skuTaken)
        {
            throw new ValidationException($"SKU \"{request.Sku}\" is already in use.");
        }

        var item = new InventoryItem
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            Sku = request.Sku,
            Name = request.Name,
            Category = request.Category,
            QuantityOnHand = request.QuantityOnHand,
            ReorderLevel = request.ReorderLevel,
            UnitCost = request.UnitCost,
            UnitPrice = request.UnitPrice
        };

        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}

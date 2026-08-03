using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Inventory.Commands;
using GymOS.Domain.Inventory;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public class InventoryImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Inventory;

    public IReadOnlyList<string> RequiredFields { get; } = ["Sku", "Name", "Category", "QuantityOnHand", "ReorderLevel"];

    public IReadOnlyList<string> OptionalFields { get; } = ["UnitCost", "UnitPrice"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
        => fields.TryGetValue("Sku", out var sku) && !string.IsNullOrWhiteSpace(sku) ? sku.Trim().ToLowerInvariant() : null;

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        foreach (var required in RequiredFields)
        {
            if (!fields.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return ImportValidationResult.Invalid($"Missing required field '{required}'.");
            }
        }

        if (!Enum.TryParse<InventoryCategory>(fields["Category"], ignoreCase: true, out _))
        {
            return ImportValidationResult.Invalid(
                $"'{fields["Category"]}' is not a valid category (expected Supplement, Merchandise, CleaningSupply, or SparePart).");
        }

        if (!int.TryParse(fields["QuantityOnHand"], out var quantity) || quantity < 0)
        {
            return ImportValidationResult.Invalid($"'{fields["QuantityOnHand"]}' is not a valid non-negative quantity.");
        }

        if (!int.TryParse(fields["ReorderLevel"], out var reorderLevel) || reorderLevel < 0)
        {
            return ImportValidationResult.Invalid($"'{fields["ReorderLevel"]}' is not a valid non-negative reorder level.");
        }

        var skuTaken = await db.InventoryItems.AnyAsync(i => i.Sku == fields["Sku"], cancellationToken);
        if (skuTaken)
        {
            return ImportValidationResult.Duplicate($"SKU '{fields["Sku"]}' already exists.");
        }

        return ImportValidationResult.Ok();
    }

    public Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var category = Enum.Parse<InventoryCategory>(fields["Category"], ignoreCase: true);
        var quantity = int.Parse(fields["QuantityOnHand"]);
        var reorderLevel = int.Parse(fields["ReorderLevel"]);
        decimal? unitCost = fields.TryGetValue("UnitCost", out var uc) && decimal.TryParse(uc, out var ucParsed) ? ucParsed : null;
        decimal? unitPrice = fields.TryGetValue("UnitPrice", out var up) && decimal.TryParse(up, out var upParsed) ? upParsed : null;

        return sender.Send(
            new CreateInventoryItemCommand(fields["Sku"], fields["Name"], category, branchId, quantity, reorderLevel, unitCost, unitPrice),
            cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), mappedEntityId);

        // InventoryItem has neither a soft-delete flag nor a status enum to deactivate, so rollback
        // hard-deletes it. Safe for the immediate-undo-of-a-fresh-import scenario this targets;
        // would fail on FK constraints if stock movements were already recorded against it.
        db.InventoryItems.Remove(item);

        await db.SaveChangesAsync(cancellationToken);
    }
}

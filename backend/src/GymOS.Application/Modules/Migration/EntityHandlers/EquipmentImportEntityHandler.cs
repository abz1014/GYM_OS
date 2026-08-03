using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Equipment.Commands;
using GymOS.Domain.Equipment;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public class EquipmentImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Equipment;

    public IReadOnlyList<string> RequiredFields { get; } = ["Name"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Category", "PurchaseDate", "PurchasePrice", "WarrantyExpiresAt", "Notes"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
        => fields.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name) ? name.Trim().ToLowerInvariant() : null;

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        if (!fields.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ImportValidationResult.Invalid("Missing required field 'Name'.");
        }

        if (fields.TryGetValue("PurchaseDate", out var purchaseDate) && !string.IsNullOrWhiteSpace(purchaseDate) && !DateOnly.TryParse(purchaseDate, out _))
        {
            return ImportValidationResult.Invalid($"'{purchaseDate}' is not a valid date for PurchaseDate (expected yyyy-MM-dd).");
        }

        if (fields.TryGetValue("WarrantyExpiresAt", out var warranty) && !string.IsNullOrWhiteSpace(warranty) && !DateOnly.TryParse(warranty, out _))
        {
            return ImportValidationResult.Invalid($"'{warranty}' is not a valid date for WarrantyExpiresAt (expected yyyy-MM-dd).");
        }

        if (fields.TryGetValue("PurchasePrice", out var price) && !string.IsNullOrWhiteSpace(price) && !decimal.TryParse(price, out _))
        {
            return ImportValidationResult.Invalid($"'{price}' is not a valid number for PurchasePrice.");
        }

        var alreadyExists = await db.Assets.AnyAsync(a => a.Name == name, cancellationToken);
        if (alreadyExists)
        {
            return ImportValidationResult.Duplicate($"An asset named '{name}' already exists.");
        }

        return ImportValidationResult.Ok();
    }

    public Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        DateOnly? purchaseDate = fields.TryGetValue("PurchaseDate", out var pd) && DateOnly.TryParse(pd, out var pdParsed) ? pdParsed : null;
        DateOnly? warrantyExpiresAt = fields.TryGetValue("WarrantyExpiresAt", out var w) && DateOnly.TryParse(w, out var wParsed) ? wParsed : null;
        decimal? purchasePrice = fields.TryGetValue("PurchasePrice", out var pp) && decimal.TryParse(pp, out var ppParsed) ? ppParsed : null;

        return sender.Send(
            new CreateAssetCommand(
                fields["Name"], fields.GetValueOrDefault("Category"), branchId, null, null,
                warrantyExpiresAt, purchaseDate, purchasePrice, fields.GetValueOrDefault("Notes")),
            cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), mappedEntityId);

        asset.Status = AssetStatus.Retired;

        await db.SaveChangesAsync(cancellationToken);
    }
}

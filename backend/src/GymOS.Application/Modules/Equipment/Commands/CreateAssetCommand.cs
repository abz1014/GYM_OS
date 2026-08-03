using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Equipment.Commands;

public record CreateAssetCommand(
    string Name, string? Category, Guid BranchId, Guid? SupplierId, string? ManualUrl,
    DateOnly? WarrantyExpiresAt, DateOnly? PurchaseDate, decimal? PurchasePrice, string? Notes) : ICommand<Guid>;

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public class CreateAssetCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateAssetCommand, Guid>
{
    public async Task<Guid> Handle(CreateAssetCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var sequence = await db.Assets.IgnoreQueryFilters().CountAsync(a => a.TenantId == tenantId, cancellationToken) + 1;

        var asset = new Asset
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            AssetTag = $"EQ-{sequence:D4}",
            Name = request.Name,
            Category = request.Category,
            QrCodeToken = Guid.NewGuid().ToString("N"),
            SupplierId = request.SupplierId,
            ManualUrl = request.ManualUrl,
            WarrantyExpiresAt = request.WarrantyExpiresAt,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            Notes = request.Notes,
            Status = AssetStatus.Active
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);

        return asset.Id;
    }
}

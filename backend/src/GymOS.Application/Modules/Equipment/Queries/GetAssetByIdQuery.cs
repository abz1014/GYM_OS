using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Equipment.Dtos;
using GymOS.Domain.Equipment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Equipment.Queries;

public record GetAssetByIdQuery(Guid Id) : IQuery<AssetDetailDto>;

public class GetAssetByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAssetByIdQuery, AssetDetailDto>
{
    public async Task<AssetDetailDto> Handle(GetAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.AsNoTracking()
            .Include(a => a.Supplier)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), request.Id);

        return new AssetDetailDto(
            asset.Id, asset.AssetTag, asset.Name, asset.Category, asset.QrCodeToken, asset.PhotoUrls, asset.ManualUrl,
            asset.WarrantyExpiresAt, asset.SupplierId, asset.Supplier?.Name, asset.Status, asset.PurchaseDate,
            asset.PurchasePrice, asset.Notes, asset.BranchId);
    }
}

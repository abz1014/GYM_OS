using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Equipment.Dtos;
using GymOS.Domain.Equipment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Equipment.Queries;

public record GetAssetsListQuery(Guid? BranchId, AssetStatus? Status, string? Category) : IQuery<List<AssetListItemDto>>;

public class GetAssetsListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAssetsListQuery, List<AssetListItemDto>>
{
    public async Task<List<AssetListItemDto>> Handle(GetAssetsListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.Assets.AsNoTracking().Include(a => a.Supplier).Where(a => accessibleBranchIds.Contains(a.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(a => a.BranchId == request.BranchId);
        }

        if (request.Status is not null)
        {
            query = query.Where(a => a.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(a => a.Category == request.Category);
        }

        return await query
            .OrderBy(a => a.AssetTag)
            .Select(a => new AssetListItemDto(a.Id, a.AssetTag, a.Name, a.Category, a.Status, a.WarrantyExpiresAt, a.Supplier!.Name))
            .ToListAsync(cancellationToken);
    }
}

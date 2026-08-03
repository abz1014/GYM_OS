using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetPermissionMatrixQuery : IQuery<PermissionMatrixDto>;

public class GetPermissionMatrixQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetPermissionMatrixQuery, PermissionMatrixDto>
{
    public async Task<PermissionMatrixDto> Handle(GetPermissionMatrixQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name))
            .ToListAsync(cancellationToken);

        var permissions = await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionCatalogEntryDto(p.Id, p.Code, p.Module, p.Description))
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(r => r.Id).ToList();
        var grants = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new RolePermissionGrantDto(rp.RoleId, rp.PermissionId))
            .ToListAsync(cancellationToken);

        return new PermissionMatrixDto(roles, permissions, grants);
    }
}

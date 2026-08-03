using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetSystemPreferencesQuery(Guid? BranchId) : IQuery<List<SystemPreferenceDto>>;

public class GetSystemPreferencesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetSystemPreferencesQuery, List<SystemPreferenceDto>>
{
    public async Task<List<SystemPreferenceDto>> Handle(GetSystemPreferencesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        return await db.SystemPreferences.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.BranchId == request.BranchId)
            .OrderBy(p => p.Key)
            .Select(p => new SystemPreferenceDto(p.Id, p.BranchId, p.Key, p.Value, p.Description))
            .ToListAsync(cancellationToken);
    }
}

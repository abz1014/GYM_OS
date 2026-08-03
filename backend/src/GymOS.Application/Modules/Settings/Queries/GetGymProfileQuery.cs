using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetGymProfileQuery : IQuery<GymProfileDto>;

public class GetGymProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetGymProfileQuery, GymProfileDto>
{
    public async Task<GymProfileDto> Handle(GetGymProfileQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var profile = await db.GymProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(GymProfile), tenantId);

        return new GymProfileDto(
            profile.LegalName, profile.DisplayName, profile.LogoUrl, profile.SupportEmail, profile.SupportPhone,
            profile.DefaultCurrency, profile.DefaultTimeZone);
    }
}

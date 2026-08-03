using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

public record UpdateGymProfileCommand(
    string LegalName, string DisplayName, string? LogoUrl, string? SupportEmail, string? SupportPhone,
    string DefaultCurrency, string DefaultTimeZone) : ICommand<Unit>;

public class UpdateGymProfileCommandValidator : AbstractValidator<UpdateGymProfileCommand>
{
    public UpdateGymProfileCommandValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty();
        RuleFor(x => x.DefaultCurrency).NotEmpty();
        RuleFor(x => x.DefaultTimeZone).NotEmpty();
    }
}

public class UpdateGymProfileCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateGymProfileCommand, Unit>
{
    public async Task<Unit> Handle(UpdateGymProfileCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var profile = await db.GymProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(GymProfile), tenantId);

        profile.LegalName = request.LegalName;
        profile.DisplayName = request.DisplayName;
        profile.LogoUrl = request.LogoUrl;
        profile.SupportEmail = request.SupportEmail;
        profile.SupportPhone = request.SupportPhone;
        profile.DefaultCurrency = request.DefaultCurrency;
        profile.DefaultTimeZone = request.DefaultTimeZone;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

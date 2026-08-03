using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth.Commands;

/// <summary>
/// The workflow's missing "enable MFA" step — ITotpService and User.MfaEnabled/MfaSecret already
/// existed and LoginCommand already checked them, but nothing ever generated a secret or flipped
/// MfaEnabled, so MFA could never actually be turned on. Two-step (initiate → confirm) so the user
/// proves their authenticator app is correctly set up before the login gate activates.
/// </summary>
public record InitiateMfaSetupCommand : ICommand<MfaSetupResultDto>;

public record MfaSetupResultDto(string Secret, string QrCodeUri);

public class InitiateMfaSetupCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ITotpService totpService)
    : IRequestHandler<InitiateMfaSetupCommand, MfaSetupResultDto>
{
    public async Task<MfaSetupResultDto> Handle(InitiateMfaSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Identity.User), currentUser.UserId ?? Guid.Empty);

        if (user.MfaEnabled)
        {
            throw new ValidationException("MFA is already enabled — disable it first to re-enroll.");
        }

        var secret = totpService.GenerateSecret();
        user.MfaSecret = secret;

        await db.SaveChangesAsync(cancellationToken);

        return new MfaSetupResultDto(secret, totpService.GenerateQrCodeUri(secret, user.Email, "GymOS"));
    }
}

public record ConfirmMfaSetupCommand(string Code) : ICommand<Unit>;

public class ConfirmMfaSetupCommandValidator : AbstractValidator<ConfirmMfaSetupCommand>
{
    public ConfirmMfaSetupCommandValidator() => RuleFor(x => x.Code).NotEmpty();
}

public class ConfirmMfaSetupCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ITotpService totpService)
    : IRequestHandler<ConfirmMfaSetupCommand, Unit>
{
    public async Task<Unit> Handle(ConfirmMfaSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Identity.User), currentUser.UserId ?? Guid.Empty);

        if (string.IsNullOrEmpty(user.MfaSecret))
        {
            throw new ValidationException("Start MFA setup before confirming it.");
        }

        if (!totpService.ValidateCode(user.MfaSecret, request.Code))
        {
            throw new ValidationException("Invalid code — check your authenticator app and try again.");
        }

        user.MfaEnabled = true;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public record DisableMfaCommand(string Password) : ICommand<Unit>;

public class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator() => RuleFor(x => x.Password).NotEmpty();
}

public class DisableMfaCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    : IRequestHandler<DisableMfaCommand, Unit>
{
    public async Task<Unit> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Identity.User), currentUser.UserId ?? Guid.Empty);

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Incorrect password.");
        }

        user.MfaEnabled = false;
        user.MfaSecret = null;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

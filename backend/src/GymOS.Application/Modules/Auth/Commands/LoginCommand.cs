using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Auditing;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth.Commands;

public record LoginCommand(string Email, string Password, string? MfaCode) : ICommand<AuthResultDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITotpService totpService,
    IDateTimeProvider dateTimeProvider,
    ILoginAttemptTracker loginAttemptTracker) : IRequestHandler<LoginCommand, AuthResultDto>
{
    private const int RefreshTokenLifetimeDays = 7;

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        // Checked before password verification on purpose: once locked, even the correct
        // password is rejected until the lockout expires — that is the entire point of a
        // lockout. Only counts failed *passwords* toward the threshold, not failed MFA codes;
        // guessing an MFA code already requires knowing the correct password first, a much
        // smaller attack surface than online password guessing. Tracked per-email via
        // ILoginAttemptTracker rather than a User column — see its doc comment for why.
        var lockedUntil = loginAttemptTracker.GetLockedUntil(request.Email, dateTimeProvider.UtcNow);
        if (lockedUntil is not null)
        {
            throw new UnauthorizedAccessException(
                $"Too many failed login attempts. Try again after {lockedUntil:HH:mm} UTC.");
        }

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null && user.IsActive)
            {
                loginAttemptTracker.RecordFailure(request.Email, dateTimeProvider.UtcNow);
            }

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.MfaCode) || !totpService.ValidateCode(user.MfaSecret!, request.MfaCode))
            {
                throw new UnauthorizedAccessException("A valid MFA code is required.");
            }
        }

        var roleNames = await UserContextLoader.GetRoleNamesAsync(db, user.Id, cancellationToken);
        var (accessToken, accessTokenExpiresAt) = jwtTokenService.GenerateAccessToken(user, roleNames);

        var rawRefreshToken = TokenHasher.GenerateRawToken();
        var refreshTokenExpiresAt = dateTimeProvider.UtcNow.AddDays(RefreshTokenLifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(rawRefreshToken),
            CreatedAt = dateTimeProvider.UtcNow,
            ExpiresAt = refreshTokenExpiresAt
        });

        user.LastLoginAt = dateTimeProvider.UtcNow;
        loginAttemptTracker.Reset(request.Email);

        await db.SaveChangesAsync(cancellationToken);

        // Anonymous endpoint — no JWT yet, so AuditBehavior can't see a TenantId. Audit it
        // directly now that the user (and their tenant) is resolved; a login is exactly the
        // kind of action Principle #4 means to cover, arguably more than most.
        await AuditLogWriter.WriteAsync(
            db, dateTimeProvider, user.TenantId, user.Id, nameof(LoginCommand), "Auth", user.Id, request, cancellationToken);

        var currentUser = await UserContextLoader.BuildAsync(db, user, cancellationToken);

        return new AuthResultDto(accessToken, accessTokenExpiresAt, rawRefreshToken, refreshTokenExpiresAt, currentUser);
    }
}

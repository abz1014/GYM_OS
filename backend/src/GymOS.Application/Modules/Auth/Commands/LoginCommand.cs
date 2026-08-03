using FluentValidation;
using GymOS.Application.Common;
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
    IDateTimeProvider dateTimeProvider) : IRequestHandler<LoginCommand, AuthResultDto>
{
    private const int RefreshTokenLifetimeDays = 7;

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
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

        await db.SaveChangesAsync(cancellationToken);

        var currentUser = await UserContextLoader.BuildAsync(db, user, cancellationToken);

        return new AuthResultDto(accessToken, accessTokenExpiresAt, rawRefreshToken, refreshTokenExpiresAt, currentUser);
    }
}

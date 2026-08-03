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

public record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResultDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private const int RefreshTokenLifetimeDays = 7;

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.RefreshToken);

        var existingToken = await db.RefreshTokens.IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive || existingToken.User is null || !existingToken.User.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var user = existingToken.User;
        var now = dateTimeProvider.UtcNow;

        var newRawRefreshToken = TokenHasher.GenerateRawToken();
        var newRefreshTokenExpiresAt = now.AddDays(RefreshTokenLifetimeDays);

        existingToken.RevokedAt = now;
        existingToken.ReplacedByTokenHash = TokenHasher.Hash(newRawRefreshToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = existingToken.ReplacedByTokenHash,
            CreatedAt = now,
            ExpiresAt = newRefreshTokenExpiresAt
        });

        var roleNames = await UserContextLoader.GetRoleNamesAsync(db, user.Id, cancellationToken);
        var (accessToken, accessTokenExpiresAt) = jwtTokenService.GenerateAccessToken(user, roleNames);

        await db.SaveChangesAsync(cancellationToken);

        // Anonymous endpoint — see LoginCommand for why this audits itself directly.
        await AuditLogWriter.WriteAsync(
            db, dateTimeProvider, user.TenantId, user.Id, nameof(RefreshTokenCommand), "Auth", user.Id, request, cancellationToken);

        var currentUser = await UserContextLoader.BuildAsync(db, user, cancellationToken);

        return new AuthResultDto(accessToken, accessTokenExpiresAt, newRawRefreshToken, newRefreshTokenExpiresAt, currentUser);
    }
}

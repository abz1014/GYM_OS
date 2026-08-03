using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth.Commands;

/// <summary>
/// Revokes the given refresh token server-side. Until this existed, "logout" only cleared local
/// tokens on the client — the refresh token stayed valid on the server for up to its full 7-day
/// lifetime, so a captured token kept working after the user believed they'd signed out.
/// </summary>
public record LogoutCommand(string RefreshToken) : ICommand<Unit>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LogoutCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.RefreshToken);

        var existingToken = await db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is not null && existingToken.IsActive)
        {
            existingToken.RevokedAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

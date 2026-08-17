using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// Issues a new one-time password for a staff account that has lost access to its old one, and hands
/// it back once for the manager to read out. There is no SMTP in this product, so the password never
/// travels — it is displayed to the person doing the reset and never stored in the clear.
/// </summary>
public record ResetStaffPasswordCommand(Guid Id) : ICommand<ResetStaffPasswordResultDto>;

public class ResetStaffPasswordCommandValidator : AbstractValidator<ResetStaffPasswordCommand>
{
    public ResetStaffPasswordCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ResetStaffPasswordCommandHandler(
    IApplicationDbContext db, IPasswordHasher passwordHasher, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResetStaffPasswordCommand, ResetStaffPasswordResultDto>
{
    public async Task<ResetStaffPasswordResultDto> Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        var temporaryPassword = TokenHasher.GenerateTemporaryPassword();
        user.PasswordHash = passwordHasher.Hash(temporaryPassword);

        /*
         * The reset also ends every session the account already has, which is the half that is easy
         * to leave out and is most of the point.
         *
         * A password is reset because the old one is suspected of being in the wrong hands. Changing
         * the hash alone stops nobody: whoever holds a live refresh token keeps rotating it into
         * fresh access tokens for another week without ever re-entering a password. Revoking here
         * means the next rotation fails and the only way back in is the new password.
         *
         * Filtered on RevokedAt IS NULL only — never on ExpiresAt, which is a DateTimeOffset and does
         * not translate on SQLite. Re-revoking an already-expired token is harmless.
         */
        var liveTokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in liveTokens)
        {
            token.RevokedAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ResetStaffPasswordResultDto(temporaryPassword);
    }
}

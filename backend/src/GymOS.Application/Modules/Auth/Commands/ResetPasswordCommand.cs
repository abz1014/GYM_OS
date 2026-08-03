using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth.Commands;

public record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Unit>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ResetPasswordCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.Token);
        var now = dateTimeProvider.UtcNow;

        var resetToken = await db.PasswordResetTokens.IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now, cancellationToken);

        if (resetToken?.User is null)
        {
            throw new UnauthorizedAccessException("Invalid or expired reset token.");
        }

        resetToken.User.PasswordHash = passwordHasher.Hash(request.NewPassword);
        resetToken.UsedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

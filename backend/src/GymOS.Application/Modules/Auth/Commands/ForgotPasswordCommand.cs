using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth.Commands;

/// <summary>
/// Demo mode: the reset link is written to NotificationLog (the in-app "Dev Mailbox") via
/// IEmailSender rather than actually emailed, so the flow is fully demoable without SMTP.
/// Always returns success regardless of whether the email exists, to avoid leaking which
/// addresses are registered.
/// </summary>
public record ForgotPasswordCommand(string Email) : ICommand<Unit>;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public class ForgotPasswordCommandHandler(
    IApplicationDbContext db,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private const int ResetTokenLifetimeHours = 2;

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Unit.Value;
        }

        var rawToken = TokenHasher.GenerateRawToken();

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(rawToken),
            CreatedAt = dateTimeProvider.UtcNow,
            ExpiresAt = dateTimeProvider.UtcNow.AddHours(ResetTokenLifetimeHours)
        });

        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(
            user.Email,
            "Reset your GymOS password",
            $"Use this token to reset your password: {rawToken}",
            cancellationToken);

        return Unit.Value;
    }
}

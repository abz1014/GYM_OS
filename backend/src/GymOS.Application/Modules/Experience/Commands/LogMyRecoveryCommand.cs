using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Commands;

/// <summary>
/// A member logging a rest / recovery day for themselves — the owner is resolved from the JWT, never
/// supplied. One recovery log per day keeps the "rest logged" signal clean; a second same-day log
/// returns the existing one rather than creating a duplicate (and the XP award is already idempotent
/// per day). Raises RecoveryLoggedEvent, which the Member Experience Engine turns into recovery XP.
/// </summary>
public record LogMyRecoveryCommand(RecoveryKind Kind, string? Notes) : ICommand<Guid>;

public class LogMyRecoveryCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogMyRecoveryCommand, Guid>
{
    public async Task<Guid> Handle(LogMyRecoveryCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var existing = await db.RecoveryLogs
            .FirstOrDefaultAsync(r => r.MemberId == memberId && r.LoggedOn == today, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var log = new RecoveryLog
        {
            MemberId = memberId,
            LoggedOn = today,
            Kind = request.Kind,
            Notes = request.Notes
        };

        db.RecoveryLogs.Add(log);
        log.RaiseLogged();
        await db.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}

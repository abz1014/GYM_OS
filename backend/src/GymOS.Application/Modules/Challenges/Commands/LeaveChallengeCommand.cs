using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Modules.Challenges.Commands;

/// <summary>A member backing out of a challenge they haven't completed. Not being joined at all is a
/// no-op (mirrors join's idempotency); a completed participation can't be left — the badge and XP are
/// already earned, so "leaving" would have nothing left to undo.</summary>
public record LeaveChallengeCommand(Guid ChallengeId) : ICommand<Unit>;

public class LeaveChallengeCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<LeaveChallengeCommand, Unit>
{
    public async Task<Unit> Handle(LeaveChallengeCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var participation = await db.ChallengeParticipants
            .FirstOrDefaultAsync(p => p.ChallengeId == request.ChallengeId && p.MemberId == memberId, cancellationToken);
        if (participation is null)
        {
            return Unit.Value;
        }

        if (participation.IsCompleted)
        {
            throw new ValidationException("A completed challenge can't be left.");
        }

        db.ChallengeParticipants.Remove(participation);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

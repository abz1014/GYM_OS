using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Services;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Challenges.Commands;

/// <summary>A member opting into a community challenge. Idempotent: joining twice is a no-op rather
/// than an error, since the member's intent ("I'm in") is already satisfied. Immediately re-checks
/// completion — if the member's workouts logged BEFORE joining already clear the target, joining
/// completes it right away rather than silently waiting for their next workout to trigger the check.</summary>
public record JoinChallengeCommand(Guid ChallengeId) : ICommand<Unit>;

public class JoinChallengeCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, IChallengeProgressService challengeProgress)
    : IRequestHandler<JoinChallengeCommand, Unit>
{
    public async Task<Unit> Handle(JoinChallengeCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var branchId = await db.Members.AsNoTracking().Where(m => m.Id == memberId).Select(m => m.BranchId).FirstAsync(cancellationToken);

        var challenge = await db.CommunityChallenges.AsNoTracking()
            .Where(c => c.Id == request.ChallengeId)
            .Select(c => new { c.BranchId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(CommunityChallenge), request.ChallengeId);

        // A member can only join a tenant-wide challenge or one scoped to their own branch — hide a
        // foreign branch's challenge as not-found, matching BookMyClassCommand's cross-branch rule.
        if (challenge.BranchId is not null && challenge.BranchId != branchId)
        {
            throw new NotFoundException(nameof(CommunityChallenge), request.ChallengeId);
        }

        var alreadyJoined = await db.ChallengeParticipants
            .AnyAsync(p => p.ChallengeId == request.ChallengeId && p.MemberId == memberId, cancellationToken);
        if (alreadyJoined)
        {
            return Unit.Value;
        }

        db.ChallengeParticipants.Add(new ChallengeParticipant
        {
            ChallengeId = request.ChallengeId,
            MemberId = memberId,
            JoinedAt = dateTimeProvider.UtcNow
        });
        // Persist the join first — ChallengeProgressService reads participations back via a plain DB
        // query (shared with the event-handler path, which always runs post-save), so it wouldn't see
        // this row yet if it ran against the still-unsaved change tracker.
        await db.SaveChangesAsync(cancellationToken);

        await challengeProgress.EvaluateAsync(memberId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

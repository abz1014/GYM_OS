using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Dtos;
using GymOS.Application.Modules.Portal;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Challenges.Queries;

/// <summary>
/// The challenges visible to the member — tenant-wide ones (BranchId null) plus their own branch's —
/// each carrying whether they've joined, completed it, and their current workout count toward its
/// goal. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyChallengesQuery : IQuery<List<MyChallengeDto>>;

public class GetMyChallengesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyChallengesQuery, List<MyChallengeDto>>
{
    public async Task<List<MyChallengeDto>> Handle(GetMyChallengesQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var branchId = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId).Select(m => (Guid?)m.BranchId).FirstOrDefaultAsync(cancellationToken);

        var challenges = await db.CommunityChallenges.AsNoTracking()
            .Where(c => c.BranchId == null || c.BranchId == branchId)
            .ToListAsync(cancellationToken);
        if (challenges.Count == 0)
        {
            return [];
        }

        var participationByChallenge = (await db.ChallengeParticipants.AsNoTracking()
                .Where(p => p.MemberId == memberId)
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.ChallengeId);

        // DateTimeOffset can't be range-filtered against a DateOnly window in SQL on SQLite, so reduce
        // to dates in memory (same pattern as Recovery/the challenge-progress event handler).
        var workoutDates = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == memberId)
                .Select(w => w.LoggedAt)
                .ToListAsync(cancellationToken))
            .Select(d => DateOnly.FromDateTime(d.UtcDateTime))
            .ToList();

        return challenges
            .Select(c =>
            {
                participationByChallenge.TryGetValue(c.Id, out var participation);
                var myCount = workoutDates.Count(d => d >= c.StartDate && d <= c.EndDate);
                return new MyChallengeDto(
                    c.Id, c.Name, c.Description, c.StartDate, c.EndDate, c.TargetWorkoutCount,
                    participation is not null, participation?.IsCompleted ?? false, myCount);
            })
            .OrderByDescending(d => d.StartDate)
            .ToList();
    }
}

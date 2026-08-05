using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Challenges.Queries;

/// <summary>Every challenge in the tenant, with aggregate join/completion counts — the staff
/// management view.</summary>
public record GetChallengesListQuery : IQuery<List<ChallengeListItemDto>>;

public class GetChallengesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetChallengesListQuery, List<ChallengeListItemDto>>
{
    public async Task<List<ChallengeListItemDto>> Handle(GetChallengesListQuery request, CancellationToken cancellationToken)
    {
        var challenges = await db.CommunityChallenges.AsNoTracking()
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);
        if (challenges.Count == 0)
        {
            return [];
        }

        var challengeIds = challenges.Select(c => c.Id).ToList();
        var counts = (await db.ChallengeParticipants.AsNoTracking()
                .Where(p => challengeIds.Contains(p.ChallengeId))
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.ChallengeId)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Completed: g.Count(p => p.IsCompleted)));

        return challenges.Select(c =>
        {
            counts.TryGetValue(c.Id, out var count);
            return new ChallengeListItemDto(
                c.Id, c.Name, c.Description, c.BranchId, c.StartDate, c.EndDate, c.TargetWorkoutCount,
                count.Total, count.Completed);
        }).ToList();
    }
}

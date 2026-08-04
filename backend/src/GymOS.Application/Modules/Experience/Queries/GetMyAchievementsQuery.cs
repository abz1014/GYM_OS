using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's full achievement shelf: every catalog achievement, flagged with whether they've
/// unlocked it (and when). Earned ones come first, then locked ones (the "what's next" preview),
/// keeping catalog order within each group. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyAchievementsQuery : IQuery<List<MyAchievementDto>>;

public class GetMyAchievementsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyAchievementsQuery, List<MyAchievementDto>>
{
    public async Task<List<MyAchievementDto>> Handle(GetMyAchievementsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var unlocked = (await db.MemberAchievements.AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .Select(a => new { a.Code, a.UnlockedAt })
            .ToListAsync(cancellationToken))
            .ToDictionary(a => a.Code, a => a.UnlockedAt);

        return AchievementCatalog.All
            .Select(a => new MyAchievementDto(
                a.Code, a.Name, a.Description, a.Tier.ToString(), a.Category.ToString(), a.Icon,
                unlocked.ContainsKey(a.Code),
                unlocked.TryGetValue(a.Code, out var at) ? at : null))
            .OrderByDescending(d => d.Unlocked)
            .ToList();
    }
}

using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Commands;

/// <summary>
/// Marks a promotion as shown, so the celebration fires once and then stops.
///
/// Every rung at or below the acknowledged one is marked too. A member who crossed two thresholds on
/// a single challenge sees the higher one — that is the arrival worth celebrating — and marking only
/// that row would leave the lower rung queued to interrupt them again on the next screen, for
/// something they have already been congratulated on.
///
/// Self-scoped like the rest of /api/me: the member is resolved from the JWT, never supplied, so this
/// cannot be used to dismiss somebody else's moment.
/// </summary>
public record AcknowledgeRankPromotionCommand(Guid PromotionId) : ICommand<Unit>;

public class AcknowledgeRankPromotionCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<AcknowledgeRankPromotionCommand, Unit>
{
    public async Task<Unit> Handle(AcknowledgeRankPromotionCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var target = await db.RankPromotions
            .FirstOrDefaultAsync(r => r.Id == request.PromotionId && r.MemberId == memberId, cancellationToken)
            ?? throw new NotFoundException(nameof(RankPromotion), request.PromotionId);

        var toMark = await db.RankPromotions
            .Where(r => r.MemberId == memberId && !r.Seen && r.Tier <= target.Tier)
            .ToListAsync(cancellationToken);

        foreach (var promotion in toMark)
        {
            promotion.Seen = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

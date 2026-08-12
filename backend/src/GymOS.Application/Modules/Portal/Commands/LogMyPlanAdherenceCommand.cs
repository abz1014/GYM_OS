using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Common;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// "I stayed on my plan today." One tap, at most once a day, worth the same XP a food diary was.
///
/// This is the whole of what the member's nutrition screen asks of them now. The screen shows what
/// the nutritionist prescribed and this confirms they followed it — which is what nutrition
/// adherence has always meant. Counting logged meals was a proxy for this, adopted because there was
/// no way to say it directly.
///
/// IDEMPOTENT, and in two layers. A second call on the same day returns the existing row rather than
/// throwing, so a double-tap on a slow connection is not an error the member has to understand; and
/// a unique index on (MemberId, OnDate) means a genuine race cannot get past the check above it.
/// The XP is idempotent independently — see AwardNutritionXpOnPlanAdherenceHandler, which shares its
/// day key with the meal path so a member doing both earns fifteen once.
///
/// The event fires only when a row is actually created. Re-raising it would be harmless today
/// (the award dedupes) and would be a trap the first time anything else listens.
/// </summary>
public record LogMyPlanAdherenceCommand(string? Note) : ICommand<Guid>;

public class LogMyPlanAdherenceCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogMyPlanAdherenceCommand, Guid>
{
    public async Task<Guid> Handle(LogMyPlanAdherenceCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // The same resolution the summary and the prescription use: the newest plan whose range
        // covers today. A member with no active plan has nothing to adhere to, and saying so is
        // better than recording a confirmation against nothing.
        var plan = (await db.DietPlans
                .Where(p => p.MemberId == memberId && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .ToListAsync(cancellationToken))
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefault();

        if (plan is null)
        {
            throw new NotFoundException("ActiveDietPlan", memberId);
        }

        var existing = await db.PlanAdherenceLogs
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.OnDate == today, cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var log = new PlanAdherenceLog
        {
            TenantId = plan.TenantId,
            MemberId = memberId,
            DietPlanId = plan.Id,
            OnDate = today,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            LoggedAt = dateTimeProvider.UtcNow
        };

        db.PlanAdherenceLogs.Add(log);

        // The ROW is stored under the gym day — that is the day the member means, and what the
        // compliance window counts. The EVENT carries the UTC day, because the meal path keys its
        // award on UTC and the two must hash identically or a member near midnight earns twice.
        plan.RaiseAdherenceLogged(DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);
        return log.Id;
    }
}

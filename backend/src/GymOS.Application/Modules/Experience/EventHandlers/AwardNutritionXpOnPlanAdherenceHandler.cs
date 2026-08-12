using GymOS.Application.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using GymOS.Domain.Nutrition;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>
/// The second way to earn nutrition XP, and deliberately indistinguishable from the first.
///
/// The member's nutrition screen stopped being a food diary, which removed the only prompt that led
/// to <see cref="MealLoggedEvent"/> — and with it the only source of NutritionAdherence XP, while the
/// rank screen carried on advertising "Follow your nutrition plan · +15" as a thing you could do.
/// A one-tap adherence confirmation restores it without asking anyone to weigh their rice.
///
/// THE KEY IS IDENTICAL TO THE MEAL HANDLER'S, and that is the whole point. MemberXpService.AwardAsync
/// dedupes on (MemberId, SourceType, SourceId, Reason), so reusing both the source type and the
/// derived `{member}:nutrition:{date}` guid means a member who ticks adherence AND logs a meal on the
/// same day earns fifteen once. Two source types would have paid them thirty.
///
/// XpSourceType.MealEntry names the nutrition-day bucket here rather than a meal row — which is what
/// it has always been in practice: the SourceId is a hash of member-and-date and has never been a
/// MealEntry id. Introducing a new enum value would have been tidier and would have split the
/// idempotency key in two, which is a real double-award against a cosmetic gain.
/// </summary>
public class AwardNutritionXpOnPlanAdherenceHandler(IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<PlanAdherenceLoggedEvent>>
{
    public Task Handle(DomainEventNotification<PlanAdherenceLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var dayKey = DeterministicGuid.From($"{e.MemberId}:nutrition:{e.OnDate:yyyy-MM-dd}");
        return xp.AwardAsync(e.MemberId, XpReason.NutritionAdherence, XpSourceType.MealEntry, dayKey, cancellationToken);
    }
}

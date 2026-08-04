using GymOS.Application.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using GymOS.Domain.Nutrition;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>Rewards nutrition consistency: a member earns nutrition XP once per day they log a meal.
/// The source key is derived from (member, consumed date), so every additional meal that day is
/// idempotent — the award lands once, not once per snack.</summary>
public class AwardNutritionXpOnMealLoggedHandler(IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<MealLoggedEvent>>
{
    public Task Handle(DomainEventNotification<MealLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var dayKey = DeterministicGuid.From($"{e.MemberId}:nutrition:{e.ConsumedDate:yyyy-MM-dd}");
        return xp.AwardAsync(e.MemberId, XpReason.NutritionAdherence, XpSourceType.MealEntry, dayKey, cancellationToken);
    }
}

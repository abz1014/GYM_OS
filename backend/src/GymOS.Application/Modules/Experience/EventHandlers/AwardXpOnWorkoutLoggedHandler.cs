using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>Awards workout-completion XP when a member logs a workout. Slice 1 grants a flat
/// completion award; progressive-improvement and PR bonuses arrive in later slices. Idempotent via
/// MemberXpService (the WorkoutLog id is the source key).</summary>
public class AwardXpOnWorkoutLoggedHandler(IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<WorkoutLoggedEvent>>
{
    public Task Handle(DomainEventNotification<WorkoutLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        return xp.AwardAsync(e.MemberId, XpReason.WorkoutCompleted, XpSourceType.WorkoutLog, e.WorkoutLogId, cancellationToken);
    }
}

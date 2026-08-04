using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>On a logged workout, updates the workout-derived projections (personal records + exercise
/// mastery). Runs alongside the XP handler on the same WorkoutLoggedEvent; both are independent and
/// idempotent.</summary>
public class ApplyWorkoutProgressionOnWorkoutLoggedHandler(IWorkoutProgressionService progression)
    : INotificationHandler<DomainEventNotification<WorkoutLoggedEvent>>
{
    public Task Handle(DomainEventNotification<WorkoutLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        return progression.ApplyWorkoutAsync(e.MemberId, e.WorkoutLogId, cancellationToken);
    }
}

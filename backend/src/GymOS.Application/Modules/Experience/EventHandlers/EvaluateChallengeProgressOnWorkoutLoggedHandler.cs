using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Services;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>
/// Re-checks a member's active challenge participations whenever they log a workout — no new domain
/// event needed, since "complete N workouts in a date window" is entirely derivable from the same
/// WorkoutLoggedEvent every other progression handler already listens to. The actual completion logic
/// lives in ChallengeProgressService, shared with JoinChallengeCommand (joining a challenge your
/// history already clears completes it immediately, rather than waiting for the next workout).
/// </summary>
public class EvaluateChallengeProgressOnWorkoutLoggedHandler(IChallengeProgressService challengeProgress)
    : INotificationHandler<DomainEventNotification<WorkoutLoggedEvent>>
{
    public Task Handle(DomainEventNotification<WorkoutLoggedEvent> notification, CancellationToken cancellationToken)
        => challengeProgress.EvaluateAsync(notification.DomainEvent.MemberId, cancellationToken);
}

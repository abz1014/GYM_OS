using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>Re-evaluates a member's achievements whenever their progression changes. Since every
/// workout and every check-in awards XP (which raises MemberProgressionChanged), this one subscription
/// covers all the triggers — and it runs after those primary writes are committed, so the stats it
/// reads are fresh.</summary>
public class EvaluateAchievementsOnProgressionChangedHandler(IAchievementService achievements)
    : INotificationHandler<DomainEventNotification<MemberProgressionChangedEvent>>
{
    public Task Handle(DomainEventNotification<MemberProgressionChangedEvent> notification, CancellationToken cancellationToken)
        => achievements.EvaluateAsync(notification.DomainEvent.MemberId, cancellationToken);
}

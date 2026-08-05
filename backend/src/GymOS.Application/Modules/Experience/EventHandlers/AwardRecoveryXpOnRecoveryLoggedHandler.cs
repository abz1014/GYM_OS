using GymOS.Application.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>Rewards recovery consistency: a member earns recovery XP once per day they log a rest /
/// recovery activity. The source key is derived from (member, logged date), so a second recovery log
/// the same day is idempotent — the award lands once, not once per stretch.</summary>
public class AwardRecoveryXpOnRecoveryLoggedHandler(IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<RecoveryLoggedEvent>>
{
    public Task Handle(DomainEventNotification<RecoveryLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var dayKey = DeterministicGuid.From($"{e.MemberId}:recovery:{e.LoggedDate:yyyy-MM-dd}");
        return xp.AwardAsync(e.MemberId, XpReason.RecoveryLogged, XpSourceType.RecoveryLog, dayKey, cancellationToken);
    }
}

using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>Awards a gym-visit XP when a member checks in (live check-ins only — historical imports
/// don't raise the event). Idempotent via MemberXpService (the AttendanceRecord id is the source
/// key), so at most one visit award per check-in.</summary>
public class AwardXpOnMemberCheckedInHandler(IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<MemberCheckedInEvent>>
{
    public Task Handle(DomainEventNotification<MemberCheckedInEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        return xp.AwardAsync(e.MemberId, XpReason.GymVisit, XpSourceType.Attendance, e.AttendanceRecordId, cancellationToken);
    }
}

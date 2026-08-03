using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Notifications.Dtos;
using GymOS.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Notifications.Queries;

public record GetScheduledNotificationsQuery(ScheduledNotificationStatus? Status = null, int Take = 100) : IQuery<List<ScheduledNotificationDto>>;

public class GetScheduledNotificationsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetScheduledNotificationsQuery, List<ScheduledNotificationDto>>
{
    public async Task<List<ScheduledNotificationDto>> Handle(GetScheduledNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.ScheduledNotifications.AsNoTracking().Include(n => n.NotificationTemplate).AsQueryable();

        if (request.Status is not null)
        {
            query = query.Where(n => n.Status == request.Status);
        }

        var notifications = await query
            .OrderByDescending(n => n.ScheduledFor)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        var memberIds = notifications.Where(n => n.RecipientMemberId is not null).Select(n => n.RecipientMemberId!.Value).Distinct().ToList();
        var userIds = notifications.Where(n => n.RecipientUserId is not null).Select(n => n.RecipientUserId!.Value).Distinct().ToList();

        var memberNames = await db.Members.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}", cancellationToken);

        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", cancellationToken);

        return notifications.Select(n => new ScheduledNotificationDto(
            n.Id,
            n.NotificationTemplate?.Code ?? "unknown",
            n.NotificationTemplate?.Category.ToString() ?? "Unknown",
            n.NotificationTemplate?.Channel.ToString() ?? "Unknown",
            n.RecipientMemberId is not null ? memberNames.GetValueOrDefault(n.RecipientMemberId.Value)
                : n.RecipientUserId is not null ? userNames.GetValueOrDefault(n.RecipientUserId.Value) : null,
            n.ScheduledFor,
            n.Status.ToString())).ToList();
    }
}

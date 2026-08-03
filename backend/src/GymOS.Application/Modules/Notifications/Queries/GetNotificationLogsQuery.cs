using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Notifications.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Notifications.Queries;

public record GetNotificationLogsQuery(int Take = 100) : IQuery<List<NotificationLogDto>>;

public class GetNotificationLogsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetNotificationLogsQuery, List<NotificationLogDto>>
{
    public async Task<List<NotificationLogDto>> Handle(GetNotificationLogsQuery request, CancellationToken cancellationToken)
        => await db.NotificationLogs.AsNoTracking()
            .OrderByDescending(l => l.SentAt)
            .Take(request.Take)
            .Select(l => new NotificationLogDto(l.Id, l.Channel.ToString(), l.RecipientAddress, l.Subject, l.Body, l.SentAt, l.Success))
            .ToListAsync(cancellationToken);
}

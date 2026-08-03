using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Notifications.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Notifications.Queries;

public record GetNotificationTemplatesQuery : IQuery<List<NotificationTemplateDto>>;

public class GetNotificationTemplatesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetNotificationTemplatesQuery, List<NotificationTemplateDto>>
{
    public async Task<List<NotificationTemplateDto>> Handle(GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
        => await db.NotificationTemplates.AsNoTracking()
            .OrderBy(t => t.Category)
            .Select(t => new NotificationTemplateDto(t.Id, t.Code, t.Category.ToString(), t.Channel.ToString(), t.Subject, t.BodyTemplate, t.IsActive))
            .ToListAsync(cancellationToken);
}

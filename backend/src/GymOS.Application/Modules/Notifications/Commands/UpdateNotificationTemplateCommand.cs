using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Notifications.Commands;

public record UpdateNotificationTemplateCommand(Guid Id, string Subject, string BodyTemplate, bool IsActive) : ICommand<Unit>;

public class UpdateNotificationTemplateCommandValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyTemplate).NotEmpty().MaximumLength(2000);
    }
}

public class UpdateNotificationTemplateCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateNotificationTemplateCommand, Unit>
{
    public async Task<Unit> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NotificationTemplate), request.Id);

        template.Subject = request.Subject;
        template.BodyTemplate = request.BodyTemplate;
        template.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

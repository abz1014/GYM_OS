using GymOS.Domain.Common;
using MediatR;

namespace GymOS.Application.Common.Messaging;

/// <summary>
/// Adapts a pure domain event into a MediatR notification so it can be dispatched to
/// <see cref="INotificationHandler{TNotification}"/>s — without the Domain layer taking a MediatR
/// dependency (it stays "entities/enums, zero external deps"). GymOsDbContext wraps each raised
/// <see cref="DomainEvent"/> in one of these when it dispatches after save; handlers implement
/// <c>INotificationHandler&lt;DomainEventNotification&lt;TDomainEvent&gt;&gt;</c>.
/// </summary>
public record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : DomainEvent;

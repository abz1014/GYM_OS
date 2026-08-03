using MediatR;

namespace GymOS.Application.Common.Messaging;

/// <summary>Marker for write requests — lets TransactionBehavior target commands only, not queries.</summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

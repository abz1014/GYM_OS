using MediatR;

namespace GymOS.Application.Common.Messaging;

public interface IQuery<TResponse> : IRequest<TResponse>;

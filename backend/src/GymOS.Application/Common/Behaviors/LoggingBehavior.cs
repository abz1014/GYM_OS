using GymOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GymOS.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation(
            "Handling {RequestName} for user {UserId} in tenant {TenantId}",
            requestName, currentUser.UserId, currentUser.TenantId);

        var response = await next(cancellationToken);

        logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}

using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using MediatR;

namespace GymOS.Application.Common.Behaviors;

/// <summary>
/// Defense-in-depth: the real tenant isolation is EF Core's global query filters in
/// GymOsDbContext, but this catches the "authenticated request with no tenant on the JWT" case
/// before a handler ever touches the database.
/// </summary>
public class TenantScopeBehavior<TRequest, TResponse>(ICurrentUserService currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (currentUser.IsAuthenticated && currentUser.TenantId is null)
        {
            throw new ForbiddenAccessException("Authenticated request is missing tenant context.");
        }

        return await next(cancellationToken);
    }
}

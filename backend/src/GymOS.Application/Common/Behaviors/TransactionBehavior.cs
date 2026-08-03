using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GymOS.Application.Common.Behaviors;

/// <summary>
/// Wraps every ICommand in a real DB transaction so multi-step handlers (e.g. creating an
/// invoice plus its lines) either fully commit or fully roll back. Queries never hit this —
/// only types implementing ICommand{TResponse} do.
/// </summary>
public class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext dbContext, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await next(cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rolling back transaction for {RequestName}", typeof(TRequest).Name);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

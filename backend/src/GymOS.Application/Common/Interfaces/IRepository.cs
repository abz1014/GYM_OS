using GymOS.Domain.Common;

namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Write-side abstraction over a single aggregate type. Query handlers read via
/// IApplicationDbContext directly for projection/join flexibility; command handlers go through
/// this so persistence details (and the tenant/branch stamping done on Add) stay out of the
/// Application layer.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}

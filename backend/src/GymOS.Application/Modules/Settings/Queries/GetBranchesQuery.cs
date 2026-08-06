using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetBranchesQuery(bool IncludeInactive = false) : IQuery<List<BranchDto>>;

public class GetBranchesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBranchesQuery, List<BranchDto>>
{
    public Task<List<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Branches.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        return query
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.AddressLine, b.City, b.Country, b.TimeZone, b.Currency, b.IsActive, b.Capacity))
            .ToListAsync(cancellationToken);
    }
}

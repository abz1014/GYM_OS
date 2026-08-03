using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetBranchesQuery : IQuery<List<BranchDto>>;

public class GetBranchesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBranchesQuery, List<BranchDto>>
{
    public Task<List<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
        => db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.City, b.Country, b.TimeZone, b.Currency, b.IsActive))
            .ToListAsync(cancellationToken);
}

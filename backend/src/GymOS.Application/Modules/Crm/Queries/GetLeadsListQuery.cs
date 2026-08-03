using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Crm.Dtos;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Queries;

public record GetLeadsListQuery(LeadStage? Stage, Guid? BranchId) : IQuery<List<LeadListItemDto>>;

public class GetLeadsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeadsListQuery, List<LeadListItemDto>>
{
    public Task<List<LeadListItemDto>> Handle(GetLeadsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Leads.AsNoTracking().AsQueryable();

        if (request.Stage is not null)
        {
            query = query.Where(l => l.Stage == request.Stage);
        }

        if (request.BranchId is not null)
        {
            query = query.Where(l => l.BranchId == request.BranchId);
        }

        return query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeadListItemDto(
                l.Id, l.FirstName + " " + l.LastName, l.Email, l.Phone, l.Source, l.Stage, l.AssignedToUserId, l.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

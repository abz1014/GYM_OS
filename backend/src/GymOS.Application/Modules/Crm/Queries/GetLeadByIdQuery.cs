using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Crm.Dtos;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Queries;

public record GetLeadByIdQuery(Guid Id) : IQuery<LeadDetailDto>;

public class GetLeadByIdQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider) : IRequestHandler<GetLeadByIdQuery, LeadDetailDto>
{
    public async Task<LeadDetailDto> Handle(GetLeadByIdQuery request, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.AsNoTracking()
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), request.Id);

        var today = dateTimeProvider.UtcNow;
        int? daysSinceLastActivity = lead.Activities.Count == 0
            ? null
            : (today.UtcDateTime.Date - lead.Activities.Max(a => a.CreatedAt).UtcDateTime.Date).Days;
        var score = LeadScorePolicy.CalculateScore(lead.Stage, lead.Source, lead.Activities.Count, daysSinceLastActivity);

        return new LeadDetailDto(
            lead.Id, lead.FirstName, lead.LastName, lead.Email, lead.Phone, lead.Source, lead.Stage,
            lead.BranchId, lead.AssignedToUserId, lead.ConvertedMemberId, lead.Notes, lead.CreatedAt, score,
            lead.Activities
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new LeadActivityDto(a.Id, a.Type, a.Notes, a.DueDate, a.CompletedAt, a.CreatedAt))
                .ToList());
    }
}

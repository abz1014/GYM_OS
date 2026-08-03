using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Crm.Dtos;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Queries;

public record GetCrmPipelineSummaryQuery(Guid? BranchId) : IQuery<CrmPipelineSummaryDto>;

public class GetCrmPipelineSummaryQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCrmPipelineSummaryQuery, CrmPipelineSummaryDto>
{
    public async Task<CrmPipelineSummaryDto> Handle(GetCrmPipelineSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = db.Leads.AsNoTracking().AsQueryable();
        if (request.BranchId is not null)
        {
            query = query.Where(l => l.BranchId == request.BranchId);
        }

        var counts = await query
            .GroupBy(l => l.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountFor(LeadStage stage) => counts.FirstOrDefault(c => c.Stage == stage)?.Count ?? 0;

        var leadCount = CountFor(LeadStage.Lead);
        var followUpCount = CountFor(LeadStage.FollowUp);
        var trialCount = CountFor(LeadStage.Trial);
        var memberCount = CountFor(LeadStage.Member);
        var lostCount = CountFor(LeadStage.Lost);

        var total = leadCount + followUpCount + trialCount + memberCount + lostCount;
        var conversionRate = total == 0 ? 0 : Math.Round(memberCount * 100.0 / total, 1);

        return new CrmPipelineSummaryDto(leadCount, followUpCount, trialCount, memberCount, lostCount, conversionRate);
    }
}

using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetCrmPipelineConversionReportQuery : IQuery<CrmPipelineConversionReportDto>;

public class GetCrmPipelineConversionReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCrmPipelineConversionReportQuery, CrmPipelineConversionReportDto>
{
    public async Task<CrmPipelineConversionReportDto> Handle(GetCrmPipelineConversionReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, cancellationToken);

    internal static async Task<CrmPipelineConversionReportDto> BuildAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var stages = await db.Leads.AsNoTracking().Select(l => l.Stage).ToListAsync(cancellationToken);

        var byStage = stages.GroupBy(s => s.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var total = stages.Count;
        var converted = stages.Count(s => s == LeadStage.Member);
        var rate = total == 0 ? 0m : Math.Round(converted * 100m / total, 1);

        return new CrmPipelineConversionReportDto(byStage, total, converted, rate);
    }
}

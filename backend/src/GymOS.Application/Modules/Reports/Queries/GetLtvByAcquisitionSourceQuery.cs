using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

/// <summary>
/// Lifetime revenue grouped by how the member found the gym — the number that answers "which
/// acquisition channel is actually worth investing in", not just "which brings in the most bodies".
/// Source is resolved with a priority order, since a member can be attributable more than one way:
/// a direct member-to-member referral (Member.ReferredByMemberId) wins first because it is the most
/// specific signal; failing that, the CRM lead they converted from carries its own LeadSource; a
/// member with neither (most historical/seed data, or a walk-in the front desk logged without a
/// CRM lead) falls into "Direct / Unattributed" rather than being silently dropped from the report.
/// </summary>
public record GetLtvByAcquisitionSourceQuery : IQuery<List<LtvBySourceRowDto>>;

public class GetLtvByAcquisitionSourceQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetLtvByAcquisitionSourceQuery, List<LtvBySourceRowDto>>
{
    public async Task<List<LtvBySourceRowDto>> Handle(GetLtvByAcquisitionSourceQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, cancellationToken);

    internal static async Task<List<LtvBySourceRowDto>> BuildAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var members = await db.Members.AsNoTracking()
            .Select(m => new { m.Id, m.ReferredByMemberId })
            .ToListAsync(cancellationToken);

        var leadSourceByMemberId = await db.Leads.AsNoTracking()
            .Where(l => l.ConvertedMemberId != null)
            .GroupBy(l => l.ConvertedMemberId!.Value)
            .Select(g => new { MemberId = g.Key, Source = g.First().Source })
            .ToDictionaryAsync(x => x.MemberId, x => x.Source, cancellationToken);

        var revenueByMemberId = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Invoice!.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Total, cancellationToken);

        string SourceLabelFor(Guid memberId, Guid? referredByMemberId)
        {
            if (referredByMemberId is not null)
            {
                return "Referral (Member)";
            }

            return leadSourceByMemberId.TryGetValue(memberId, out var leadSource)
                ? leadSource.ToString()
                : "Direct / Unattributed";
        }

        return members
            .Select(m => new { Source = SourceLabelFor(m.Id, m.ReferredByMemberId), Revenue = revenueByMemberId.GetValueOrDefault(m.Id) })
            .GroupBy(x => x.Source)
            .Select(g => new LtvBySourceRowDto(
                g.Key,
                g.Count(),
                g.Sum(x => x.Revenue),
                g.Count() == 0 ? 0m : Math.Round(g.Sum(x => x.Revenue) / g.Count(), 2)))
            .OrderByDescending(r => r.TotalRevenue)
            .ToList();
    }
}

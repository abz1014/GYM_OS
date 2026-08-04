using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Crm.Dtos;
using GymOS.Domain.Crm;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Queries;

public record GetLeadsListQuery(LeadStage? Stage, Guid? BranchId, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<LeadListItemDto>>;

public class GetLeadsListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetLeadsListQuery, PagedList<LeadListItemDto>>
{
    public async Task<PagedList<LeadListItemDto>> Handle(GetLeadsListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.Leads.AsNoTracking().Where(l => accessibleBranchIds.Contains(l.BranchId));

        if (request.Stage is not null)
        {
            query = query.Where(l => l.Stage == request.Stage);
        }

        if (request.BranchId is not null)
        {
            query = query.Where(l => l.BranchId == request.BranchId);
        }

        // CreatedAt (DateTimeOffset) can't be ordered in SQL on SQLite (the in-memory test provider),
        // and a tenant's lead pipeline is a bounded, modest-sized set — pull the accessible/filtered
        // set once and order/page it client-side, the same trade-off already made for at-risk
        // members and waitlist/roster ordering elsewhere in this codebase.
        var allMatching = await query
            .Select(l => new
            {
                l.Id, l.FirstName, l.LastName, l.Email, l.Phone, l.Source, l.Stage, l.AssignedToUserId, l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var ordered = allMatching.OrderByDescending(l => l.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var page = ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        // One query for every activity belonging to this page's leads, rather than one query per
        // lead — the count/recency signal is then reduced in memory per lead.
        var pageLeadIds = page.Select(l => l.Id).ToList();
        var activities = await db.LeadActivities.AsNoTracking()
            .Where(a => pageLeadIds.Contains(a.LeadId))
            .Select(a => new { a.LeadId, a.CreatedAt })
            .ToListAsync(cancellationToken);
        var activitiesByLeadId = activities.GroupBy(a => a.LeadId).ToDictionary(g => g.Key, g => g.Select(a => a.CreatedAt).ToList());

        var today = dateTimeProvider.UtcNow;
        var items = page.Select(l =>
        {
            var (activityCount, daysSinceLastActivity) = ActivitySignal(activitiesByLeadId.GetValueOrDefault(l.Id), today);
            var score = LeadScorePolicy.CalculateScore(l.Stage, l.Source, activityCount, daysSinceLastActivity);
            return new LeadListItemDto(l.Id, $"{l.FirstName} {l.LastName}", l.Email, l.Phone, l.Source, l.Stage, l.AssignedToUserId, l.CreatedAt, score);
        }).ToList();

        return new PagedList<LeadListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    internal static (int ActivityCount, int? DaysSinceLastActivity) ActivitySignal(List<DateTimeOffset>? activityTimestamps, DateTimeOffset today)
    {
        if (activityTimestamps is null || activityTimestamps.Count == 0)
        {
            return (0, null);
        }

        var lastActivity = activityTimestamps.Max();
        var daysSince = (today.UtcDateTime.Date - lastActivity.UtcDateTime.Date).Days;
        return (activityTimestamps.Count, daysSince);
    }
}

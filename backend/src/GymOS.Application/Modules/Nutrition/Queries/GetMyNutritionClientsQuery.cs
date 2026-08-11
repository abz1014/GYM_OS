using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Queries;

/// <summary>
/// The nutritionist's own people.
///
/// Every other specialist in this product knows who they are responsible for. A trainer opens My
/// clients and sees a roster, because TrainerAssignment makes the relationship a real thing. A
/// nutritionist had no equivalent — no assignment entity, no field, nothing — so the nutrition screen
/// could only offer a search box reading "Search member to view or log their nutrition", which
/// requires already knowing the name of the person you are looking for. That is not a workflow; it is
/// a lookup, and it is why nutrition felt like a module rather than a job.
///
/// The relationship was already in the data and unread: DietPlan.CreatedByUserId records who wrote
/// each plan. A nutritionist's clients are the members they have written a plan for. No new table, no
/// migration — the same move as the coaching context bar, which found the trainer's signals sitting
/// on a screen the trainer never opened.
///
/// Deliberately NOT scoped to plans that are still running. A plan that expired last month is the
/// clearest thing on this screen: the member is still theirs and the prescription lapsed. Filtering
/// to active plans would make people disappear from the roster at exactly the moment they need it.
/// </summary>
public record GetMyNutritionClientsQuery : IQuery<MyNutritionClientsDto>;

public class GetMyNutritionClientsQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyNutritionClientsQuery, MyNutritionClientsDto>
{
    public async Task<MyNutritionClientsDto> Handle(
        GetMyNutritionClientsQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var weekAgo = dateTimeProvider.UtcNow.AddDays(-7);

        // Held in a local rather than compared inline. CreatedByUserId is nullable, so an
        // unauthenticated UserId of null would quietly match every plan whose author was never
        // recorded — a roster assembled out of other people's orphans.
        var authorId = currentUser.UserId;
        if (authorId is null) return new MyNutritionClientsDto([], 0);

        // Joined by hand: DietPlan holds a MemberId but no navigation, and the member's own branch
        // filter has to apply to that side of the join — a plan written for a member in another
        // branch must not surface a name the caller is not entitled to read.
        var plans = await (
            from p in db.DietPlans.AsNoTracking()
            join m in db.Members.AsNoTracking() on p.MemberId equals m.Id
            where p.CreatedByUserId == authorId
            select new
            {
                p.Id,
                p.MemberId,
                p.Name,
                p.StartDate,
                p.EndDate,
                MemberName = m.FirstName + " " + m.LastName,
                m.MemberCode
            }).ToListAsync(cancellationToken);

        // One row per member, not per plan: a member re-prescribed three times is one client with
        // the newest plan showing, the same rule the trainer roster uses for repeat pairings.
        var newestPerMember = plans
            .GroupBy(p => p.MemberId)
            .Select(g => g.OrderByDescending(p => p.StartDate).First())
            .ToList();

        var planIds = newestPerMember.Select(p => p.Id).ToList();

        /*
         * Meal timestamps come back to memory before being compared. ConsumedAt is a DateTimeOffset,
         * and SQLite translates neither an ORDER BY nor a range filter on one — the same constraint
         * the member timeline documents. Bounded by the roster, which is one nutritionist's clients.
         */
        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => planIds.Contains(m.DietPlanId) && m.ConsumedAt != null)
            .Select(m => new { m.DietPlanId, m.ConsumedAt })
            .ToListAsync(cancellationToken);

        var mealsByPlan = meals.GroupBy(m => m.DietPlanId).ToDictionary(g => g.Key, g => g.ToList());

        var clients = newestPerMember
            .Select(p =>
            {
                var mine = mealsByPlan.GetValueOrDefault(p.Id) ?? [];
                return new NutritionClientRowDto(
                    p.MemberId,
                    p.MemberName,
                    p.MemberCode,
                    p.Name,
                    p.StartDate,
                    p.EndDate,
                    p.StartDate <= today && (p.EndDate is null || p.EndDate >= today),
                    mine.Count == 0 ? null : mine.Max(m => m.ConsumedAt),
                    mine.Count(m => m.ConsumedAt >= weekAgo));
            })
            // Lapsed plans first — they are the work. Within each group, the quietest member first,
            // because someone who has logged nothing is the one worth a message.
            .OrderBy(c => c.PlanIsActive)
            .ThenBy(c => c.LastMealLoggedAt ?? DateTimeOffset.MinValue)
            .ToList();

        /*
         * Members nobody is feeding. Counted across ALL nutritionists, not just this one: "how many
         * members have no plan" is a fact about the gym, and answering it per-author would report
         * every one of a colleague's clients as unserved.
         */
        var membersWithAnyActivePlan = await db.DietPlans.AsNoTracking()
            .Where(p => p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
            .Select(p => p.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeMembers = await db.Members.AsNoTracking()
            .Where(m => m.Status == MemberStatus.Active)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var unserved = activeMembers.Count(id => !membersWithAnyActivePlan.Contains(id));

        return new MyNutritionClientsDto(clients, unserved);
    }
}

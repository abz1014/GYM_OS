using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Today's macro totals against the member's active diet plan target — the number a member
/// actually checks mid-day ("how much protein do I have left"), not just a historical log. The
/// active plan is whichever one covers today's date; if more than one somehow does, the most
/// recently started wins. Consumed totals come only from meal entries actually marked consumed
/// today — a planned-but-not-yet-eaten entry (ConsumedAt null) doesn't count toward the day.
/// </summary>
public record GetMyNutritionSummaryQuery : IQuery<MyNutritionSummaryDto>;

public class GetMyNutritionSummaryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyNutritionSummaryQuery, MyNutritionSummaryDto>
{
    public async Task<MyNutritionSummaryDto> Handle(GetMyNutritionSummaryQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var activePlan = await db.DietPlans.AsNoTracking()
            .Where(p => p.MemberId == memberId && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePlan is null)
        {
            return new MyNutritionSummaryDto(null, null, null, null, null, 0, 0, 0, 0, 0);
        }

        // Pull today's consumed entries with the food item's per-serving macros, then multiply by
        // quantity in memory — a member's daily entry count is small, and this avoids translating a
        // DateTimeOffset.Date comparison that behaves differently across providers.
        var entries = await db.MealEntries.AsNoTracking()
            .Where(e => e.DietPlanId == activePlan.Id && e.ConsumedAt != null)
            .Select(e => new { e.Quantity, e.ConsumedAt, e.FoodItemId })
            .ToListAsync(cancellationToken);

        var todaysEntries = entries.Where(e => GymDay.Of(e.ConsumedAt!.Value, zone) == today).ToList();
        var foodItemIds = todaysEntries.Select(e => e.FoodItemId).Distinct().ToList();
        var foodItems = await db.FoodItems.AsNoTracking()
            .Where(f => foodItemIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        decimal calories = 0, protein = 0, carbs = 0, fat = 0;
        foreach (var entry in todaysEntries)
        {
            if (!foodItems.TryGetValue(entry.FoodItemId, out var food))
            {
                continue;
            }

            calories += food.CaloriesPerServing * entry.Quantity;
            protein += food.ProteinG * entry.Quantity;
            carbs += food.CarbsG * entry.Quantity;
            fat += food.FatG * entry.Quantity;
        }

        var waterLogs = await db.WaterLogs.AsNoTracking()
            .Where(w => w.MemberId == memberId)
            .Select(w => new { w.AmountMl, w.LoggedAt })
            .ToListAsync(cancellationToken);
        var waterToday = waterLogs.Where(w => GymDay.Of(w.LoggedAt, zone) == today).Sum(w => w.AmountMl);

        return new MyNutritionSummaryDto(
            activePlan.Name, activePlan.TargetCalories, activePlan.TargetProteinG, activePlan.TargetCarbsG, activePlan.TargetFatG,
            calories, protein, carbs, fat, waterToday);
    }
}

using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetNutritionReportQuery(int DaysBack = 30) : IQuery<NutritionReportDto>;

public class GetNutritionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetNutritionReportQuery, NutritionReportDto>
{
    public async Task<NutritionReportDto> Handle(GetNutritionReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

    internal static async Task<NutritionReportDto> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int daysBack, CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow.AddDays(-daysBack);

        // MealEntry isn't tenant-scoped itself — joining through FoodItem (which is) naturally
        // restricts results to the current tenant, the same pattern CommissionRecord/Trainers
        // relies on in GetTrainerCommissionReportQuery.
        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => m.ConsumedAt != null && m.ConsumedAt >= cutoff)
            .Join(db.FoodItems, m => m.FoodItemId, f => f.Id, (m, f) => new { m.Quantity, f.Name, f.CaloriesPerServing })
            .ToListAsync(cancellationToken);

        var topFoodItems = meals
            .GroupBy(m => m.Name)
            .Select(g => new NutritionFoodItemRowDto(g.Key, g.Count(), Math.Round(g.Sum(x => x.Quantity * x.CaloriesPerServing), 0)))
            .OrderByDescending(r => r.TimesLogged)
            .ToList();

        // WaterLog has no tenant-scoped join partner (only a bare MemberId) — restricting to the
        // tenant's known Member ids mirrors how DowntimeLog is scoped in GetEquipmentDowntimeReportQuery.
        var memberIds = (await db.Members.AsNoTracking().Select(m => m.Id).ToListAsync(cancellationToken)).ToHashSet();
        var waterLogs = (await db.WaterLogs.AsNoTracking()
                .Where(w => w.LoggedAt >= cutoff)
                .Select(w => new { w.MemberId, w.AmountMl })
                .ToListAsync(cancellationToken))
            .Where(w => memberIds.Contains(w.MemberId))
            .ToList();

        return new NutritionReportDto(
            topFoodItems,
            meals.Count,
            Math.Round(meals.Sum(m => m.Quantity * m.CaloriesPerServing), 0),
            waterLogs.Count,
            waterLogs.Sum(w => w.AmountMl));
    }
}

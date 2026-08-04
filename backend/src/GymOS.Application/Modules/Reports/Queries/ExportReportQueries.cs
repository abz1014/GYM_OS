using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using MediatR;

namespace GymOS.Application.Modules.Reports.Queries;

public record ExportRevenueReportQuery(int MonthsBack = 6) : IQuery<byte[]>;

public class ExportRevenueReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportRevenueReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportRevenueReportQuery request, CancellationToken cancellationToken)
    {
        var points = await GetRevenueReportQueryHandler.BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Revenue",
            ["Period", "Revenue (USD)"],
            points.Select(p => (IReadOnlyList<object?>)[p.Period, p.Revenue]).ToList());
    }
}

public record ExportAttendanceReportQuery(int DaysBack = 30) : IQuery<byte[]>;

public class ExportAttendanceReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportAttendanceReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportAttendanceReportQuery request, CancellationToken cancellationToken)
    {
        var points = await GetAttendanceReportQueryHandler.BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Attendance",
            ["Date", "Check-ins"],
            points.Select(p => (IReadOnlyList<object?>)[p.Date, p.CheckIns]).ToList());
    }
}

public record ExportMembershipReportQuery : IQuery<byte[]>;

public class ExportMembershipReportQueryHandler(IApplicationDbContext db, IReportExporter exporter)
    : IRequestHandler<ExportMembershipReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportMembershipReportQuery request, CancellationToken cancellationToken)
    {
        var breakdown = await GetMembershipBreakdownQueryHandler.BuildAsync(db, cancellationToken);

        var rows = new List<IReadOnlyList<object?>>();
        rows.AddRange(breakdown.ByStatus.Select(kv => (IReadOnlyList<object?>)["By Status", kv.Key, kv.Value]));
        rows.AddRange(breakdown.ByPlanType.Select(kv => (IReadOnlyList<object?>)["By Plan Type", kv.Key, kv.Value]));

        return exporter.ExportToXlsx("Memberships", ["Breakdown", "Category", "Count"], rows);
    }
}

public record ExportTrainerCommissionReportQuery(int MonthsBack = 6) : IQuery<byte[]>;

public class ExportTrainerCommissionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportTrainerCommissionReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportTrainerCommissionReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetTrainerCommissionReportQueryHandler.BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Trainer Commissions",
            ["Trainer", "Pending (USD)", "Paid (USD)", "Records"],
            rows.Select(r => (IReadOnlyList<object?>)[r.TrainerName, r.TotalPending, r.TotalPaid, r.RecordCount]).ToList());
    }
}

public record ExportEquipmentDowntimeReportQuery(int MonthsBack = 6) : IQuery<byte[]>;

public class ExportEquipmentDowntimeReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportEquipmentDowntimeReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportEquipmentDowntimeReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetEquipmentDowntimeReportQueryHandler.BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Equipment Downtime",
            ["Asset", "Tag", "Incidents", "Downtime (hrs)", "Maintenance Cost (USD)"],
            rows.Select(r => (IReadOnlyList<object?>)[r.AssetName, r.AssetTag, r.Incidents, Math.Round(r.TotalDowntimeHours, 1), r.TotalMaintenanceCost]).ToList());
    }
}

public record ExportInventoryStockMovementReportQuery(int DaysBack = 30) : IQuery<byte[]>;

public class ExportInventoryStockMovementReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportInventoryStockMovementReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportInventoryStockMovementReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetInventoryStockMovementReportQueryHandler.BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Stock Movement",
            ["Item", "SKU", "Total In", "Total Out", "Net Change", "On Hand"],
            rows.Select(r => (IReadOnlyList<object?>)[r.ItemName, r.Sku, r.TotalIn, r.TotalOut, r.NetChange, r.CurrentQuantityOnHand]).ToList());
    }
}

public record ExportCrmPipelineConversionReportQuery : IQuery<byte[]>;

public class ExportCrmPipelineConversionReportQueryHandler(IApplicationDbContext db, IReportExporter exporter)
    : IRequestHandler<ExportCrmPipelineConversionReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportCrmPipelineConversionReportQuery request, CancellationToken cancellationToken)
    {
        var report = await GetCrmPipelineConversionReportQueryHandler.BuildAsync(db, cancellationToken);

        var rows = report.ByStage.Select(kv => (IReadOnlyList<object?>)["By Stage", kv.Key, kv.Value]).ToList();
        rows.Add(["Summary", "Total Leads", report.TotalLeads]);
        rows.Add(["Summary", "Converted", report.ConvertedCount]);
        rows.Add(["Summary", "Conversion Rate (%)", report.ConversionRatePercent]);

        return exporter.ExportToXlsx("CRM Pipeline", ["Section", "Category", "Value"], rows);
    }
}

public record ExportWorkoutActivityReportQuery(int DaysBack = 30) : IQuery<byte[]>;

public class ExportWorkoutActivityReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportWorkoutActivityReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportWorkoutActivityReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetWorkoutActivityReportQueryHandler.BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Workout Activity",
            ["Exercise", "Muscle Group", "Times Logged", "Total Sets", "Total Reps", "Avg Weight (kg)"],
            rows.Select(r => (IReadOnlyList<object?>)[r.ExerciseName, r.MuscleGroup, r.TimesLogged, r.TotalSets, r.TotalReps, r.AvgWeightKg]).ToList());
    }
}

public record ExportNutritionReportQuery(int DaysBack = 30) : IQuery<byte[]>;

public class ExportNutritionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportNutritionReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportNutritionReportQuery request, CancellationToken cancellationToken)
    {
        var report = await GetNutritionReportQueryHandler.BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

        var rows = report.TopFoodItems
            .Select(r => (IReadOnlyList<object?>)["Food Item", r.FoodItemName, r.TimesLogged, r.TotalCaloriesLogged])
            .ToList();
        rows.Add(["Summary", "Total Meal Entries Logged", report.TotalMealEntriesLogged, null]);
        rows.Add(["Summary", "Total Calories Logged", report.TotalCaloriesLogged, null]);
        rows.Add(["Summary", "Total Water Logs", report.TotalWaterLogsLogged, null]);
        rows.Add(["Summary", "Total Water (ml)", report.TotalWaterMlLogged, null]);

        return exporter.ExportToXlsx("Nutrition", ["Section", "Category", "Times Logged", "Calories"], rows);
    }
}

public record ExportAtRiskMembersReportQuery : IQuery<byte[]>;

public class ExportAtRiskMembersReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportAtRiskMembersReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportAtRiskMembersReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetAtRiskMembersReportQueryHandler.BuildAsync(db, dateTimeProvider, cancellationToken);

        return exporter.ExportToXlsx(
            "At-Risk Members",
            ["Member", "Code", "Last Check-in", "Days Since Last Visit"],
            rows.Select(r => (IReadOnlyList<object?>)[r.FullName, r.MemberCode, r.LastCheckInDate, r.DaysSinceLastVisit]).ToList());
    }
}

public record ExportCohortRetentionReportQuery(int MonthsBack = 12) : IQuery<byte[]>;

public class ExportCohortRetentionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, IReportExporter exporter)
    : IRequestHandler<ExportCohortRetentionReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportCohortRetentionReportQuery request, CancellationToken cancellationToken)
    {
        var points = await GetCohortRetentionReportQueryHandler.BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

        return exporter.ExportToXlsx(
            "Cohort Retention",
            ["Join Month", "Cohort Size", "Still Active", "Retention Rate (%)"],
            points.Select(p => (IReadOnlyList<object?>)[p.CohortMonth, p.CohortSize, p.StillActiveCount, p.RetentionRatePercent]).ToList());
    }
}

public record ExportLtvByAcquisitionSourceQuery : IQuery<byte[]>;

public class ExportLtvByAcquisitionSourceQueryHandler(IApplicationDbContext db, IReportExporter exporter)
    : IRequestHandler<ExportLtvByAcquisitionSourceQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportLtvByAcquisitionSourceQuery request, CancellationToken cancellationToken)
    {
        var rows = await GetLtvByAcquisitionSourceQueryHandler.BuildAsync(db, cancellationToken);

        return exporter.ExportToXlsx(
            "LTV by Acquisition Source",
            ["Source", "Members", "Total Revenue (USD)", "Average LTV (USD)"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Source, r.MemberCount, r.TotalRevenue, r.AverageLtv]).ToList());
    }
}

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

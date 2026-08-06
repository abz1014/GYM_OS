using GymOS.Application.Modules.Reports.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Reports;

/// <summary>
/// The capture-rate report is the instrument the member-experience roadmap is judged with, so what
/// it counts — and when it admits it can't be trusted — is pinned here.
///
/// Clock fixed to Thursday 2026-08-06; that week runs Mon 2026-08-03 .. Sun 2026-08-09.
/// </summary>
public class LoggingCaptureReportTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Thursday = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    public LoggingCaptureReportTests() => DateTimeProvider.UtcNow = Thursday;

    [Fact]
    public async Task Reports_the_share_of_visits_that_produced_a_workout()
    {
        var ctx = await SeedAsync();
        // Four visits, two of which were logged.
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await LogAsync(ctx.MemberId, Thursday);
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-1));
        await LogAsync(ctx.MemberId, Thursday.AddDays(-1));
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-2));
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-3));
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.TotalVisitDays.ShouldBe(4);
        report.TotalLoggedVisitDays.ShouldBe(2);
        report.CaptureRatePercent.ShouldBe(50);
    }

    [Fact]
    public async Task Counts_days_not_rows_so_a_double_swipe_cannot_move_the_rate()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddHours(6));   // same day, scanned twice
        await LogAsync(ctx.MemberId, Thursday);
        await LogAsync(ctx.MemberId, Thursday.AddHours(1));          // split the session in two
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.TotalVisitDays.ShouldBe(1);
        report.TotalLoggedVisitDays.ShouldBe(1);
        report.CaptureRatePercent.ShouldBe(100);
    }

    [Fact]
    public async Task A_gym_where_nobody_logs_reports_zero_rather_than_failing()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-1));
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.CaptureRatePercent.ShouldBe(0);
        report.TotalVisitDays.ShouldBe(2);
        report.MembersVisitingWithoutLogging.ShouldBe(1);
    }

    [Fact]
    public async Task An_empty_gym_does_not_divide_by_zero()
    {
        var ctx = await SeedAsync();
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.CaptureRatePercent.ShouldBe(0);
        report.TotalVisitDays.ShouldBe(0);
        report.IsReliable.ShouldBeTrue();   // nothing happened is not the same as something wrong
    }

    [Fact]
    public async Task Workouts_logged_with_no_visit_are_flagged_rather_than_counted_as_captured()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await LogAsync(ctx.MemberId, Thursday);
        // Logged at home — no visit on record.
        await LogAsync(ctx.MemberId, Thursday.AddDays(-2));
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.TotalOrphanLogDays.ShouldBe(1);
        report.TotalLoggedVisitDays.ShouldBe(1);
        report.CaptureRatePercent.ShouldBe(100);   // of the visits that happened, all were captured
    }

    [Fact]
    public async Task Mostly_off_site_logging_marks_the_rate_unreliable()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await LogAsync(ctx.MemberId, Thursday);
        for (var d = 1; d <= 5; d++)
        {
            await LogAsync(ctx.MemberId, Thursday.AddDays(-d));   // five logs, no visits
        }
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.IsReliable.ShouldBeFalse();
    }

    [Fact]
    public async Task Splits_the_window_into_monday_start_weeks()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday);                 // this week
        await LogAsync(ctx.MemberId, Thursday);
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-7));     // last week, unlogged
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(3));

        report.Weekly.Count.ShouldBe(3);
        report.Weekly.Select(w => w.WeekStart).ShouldBe(
            [new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 27), new DateOnly(2026, 8, 3)]);
        report.Weekly[^1].CaptureRatePercent.ShouldBe(100);   // current week: 1 of 1
        report.Weekly[^2].CaptureRatePercent.ShouldBe(0);     // previous week: 0 of 1
    }

    [Fact]
    public async Task Separates_members_who_log_from_members_who_only_turn_up()
    {
        var ctx = await SeedAsync();
        var quiet = await AddMemberAsync(ctx);
        await VisitAsync(ctx, ctx.MemberId, Thursday);
        await LogAsync(ctx.MemberId, Thursday);
        await VisitAsync(ctx, quiet, Thursday);
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(4));

        report.MembersWhoVisited.ShouldBe(2);
        report.MembersWhoLogged.ShouldBe(1);
        report.MembersVisitingWithoutLogging.ShouldBe(1);
    }

    [Fact]
    public async Task Activity_older_than_the_window_is_excluded()
    {
        var ctx = await SeedAsync();
        await VisitAsync(ctx, ctx.MemberId, Thursday.AddDays(-60));
        await LogAsync(ctx.MemberId, Thursday.AddDays(-60));
        AsStaff(ctx);

        var report = await SendAsync(new GetLoggingCaptureReportQuery(2));

        report.TotalVisitDays.ShouldBe(0);
    }

    private void AsStaff(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task VisitAsync(SeedContext ctx, Guid memberId, DateTimeOffset at)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, MemberId = memberId,
            CheckInAt = at, Method = AttendanceMethod.Manual
        });
        await db.SaveChangesAsync();
    }

    private async Task LogAsync(Guid memberId, DateTimeOffset at)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.WorkoutLogs.Add(new WorkoutLog { MemberId = memberId, LoggedAt = at });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddMemberAsync(SeedContext ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = new Member
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Quiet", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId, Guid StaffUserId);

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var staff = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Staff", LastName = "User"
        };
        db.Users.Add(staff);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staff.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, member.Id, staff.Id);
    }
}

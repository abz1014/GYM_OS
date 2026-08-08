using GymOS.Application.Modules.Reports.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Reports;

/// <summary>
/// Week-N return through the real pipeline. The arithmetic is covered in ReturnRatePolicyTests; what
/// is pinned here is the thing that would make the number lie in production — a gym that just signed
/// a lot of members must not read as a gym that just lost a lot of members.
///
/// Clock fixed to 2026-08-06.
/// </summary>
public class ReturnRateReportTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Today = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly TodayDate = new(2026, 8, 6);

    public ReturnRateReportTests() => DateTimeProvider.UtcNow = Today;

    [Fact]
    public async Task A_member_who_joined_yesterday_is_absent_from_every_cohort_rather_than_counted_as_lost()
    {
        var ctx = await SeedAsync();
        await AddMemberAsync(ctx, joinDate: TodayDate.AddDays(-1));
        AsStaff(ctx);

        var report = await SendAsync(new GetReturnRateReportQuery());

        // Not "0% returned" — nobody is eligible yet, and an empty denominator has to be visible as
        // such. This is the whole reason ReturnRatePolicy separates eligibility from outcome.
        report.Weeks.ShouldAllBe(w => w.EligibleMembers == 0);
        report.Weeks.ShouldAllBe(w => w.ReturnedMembers == 0);
    }

    [Fact]
    public async Task A_member_who_trained_in_their_second_week_counts_as_returned()
    {
        var ctx = await SeedAsync();
        var joined = TodayDate.AddDays(-30);
        var memberId = await AddMemberAsync(ctx, joined);

        // Day 9 after joining — inside week 2 (days 7-13).
        await VisitAsync(ctx, memberId, Today.AddDays(-21));
        AsStaff(ctx);

        var report = await SendAsync(new GetReturnRateReportQuery());
        var weekTwo = report.Weeks.Single(w => w.WeekNumber == 2);

        weekTwo.EligibleMembers.ShouldBe(1);
        weekTwo.ReturnedMembers.ShouldBe(1);
        weekTwo.RatePercent.ShouldBe(100);
    }

    [Fact]
    public async Task Training_only_in_the_joining_week_does_not_count_as_returning_in_week_two()
    {
        var ctx = await SeedAsync();
        var joined = TodayDate.AddDays(-30);
        var memberId = await AddMemberAsync(ctx, joined);

        // Day 2 after joining — still week 1, the honeymoon visit. The question week 2 asks is
        // whether they came back after that, and this member did not.
        await VisitAsync(ctx, memberId, Today.AddDays(-28));
        AsStaff(ctx);

        var report = await SendAsync(new GetReturnRateReportQuery());
        var weekTwo = report.Weeks.Single(w => w.WeekNumber == 2);

        weekTwo.EligibleMembers.ShouldBe(1);
        weekTwo.ReturnedMembers.ShouldBe(0);
        weekTwo.RatePercent.ShouldBe(0);
    }

    [Fact]
    public async Task A_growing_gym_does_not_show_collapsing_week_twelve_retention()
    {
        var ctx = await SeedAsync();

        // One long-standing member who did come back in week 12...
        var established = await AddMemberAsync(ctx, TodayDate.AddDays(-200));
        await VisitAsync(ctx, established, Today.AddDays(-123)); // day 77 = start of week 12

        // ...and a rush of ten new sign-ups this month, none of whom can have a week 12 yet.
        for (var i = 0; i < 10; i++)
        {
            await AddMemberAsync(ctx, TodayDate.AddDays(-(i + 1)));
        }

        AsStaff(ctx);

        var report = await SendAsync(new GetReturnRateReportQuery());
        var weekTwelve = report.Weeks.Single(w => w.WeekNumber == 12);

        // The ten newcomers are simply not in this question yet. Counting them would report 1/11 = 9%
        // and make the gym's best month look like its worst.
        weekTwelve.EligibleMembers.ShouldBe(1);
        weekTwelve.ReturnedMembers.ShouldBe(1);
        weekTwelve.RatePercent.ShouldBe(100);
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

    private async Task<Guid> AddMemberAsync(SeedContext ctx, DateOnly joinDate)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = new Member
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = joinDate,
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

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

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, staff.Id);
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid StaffUserId);
}

using GymOS.Application.Modules.Attendance.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Attendance;

/// <summary>
/// Checking the same person in twice.
///
/// Nothing stopped that, and it is the easiest thing in the world to trigger — a counter scanner
/// firing twice on one swipe, a member scanning again because the screen didn't change, a
/// receptionist pressing the button twice. Each duplicate opened another row with no check-out, so
/// the desk's "in the building" count climbed all day and never came back down.
///
/// The clock is fixed per test; the branch is UTC unless a test says otherwise.
/// </summary>
public class CheckInCommandTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Afternoon = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_second_check_in_returns_the_visit_they_are_already_on()
    {
        var ctx = await SeedAsync();
        AsStaff(ctx);
        DateTimeProvider.UtcNow = Afternoon;

        var first = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));
        var second = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        second.ShouldBe(first);
        (await CountVisitsAsync(ctx.MemberId)).ShouldBe(1);
    }

    [Fact]
    public async Task Coming_back_after_checking_out_is_a_real_second_visit()
    {
        // The guard is about being INSIDE, not about having been in today. Someone who trained at
        // lunch and came back in the evening has genuinely visited twice.
        var ctx = await SeedAsync();
        AsStaff(ctx);
        DateTimeProvider.UtcNow = Afternoon;

        var first = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));
        await SendAsync(new CheckOutCommand(first));

        DateTimeProvider.UtcNow = Afternoon.AddHours(5);
        var second = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        second.ShouldNotBe(first);
        (await CountVisitsAsync(ctx.MemberId)).ShouldBe(2);
    }

    [Fact]
    public async Task A_visit_nobody_ever_closed_does_not_block_them_forever()
    {
        // The failure mode that makes a naive "any open row" guard worse than no guard: members
        // routinely leave without swiping out, and that stale row must not refuse every visit after.
        var ctx = await SeedAsync();
        AsStaff(ctx);

        DateTimeProvider.UtcNow = Afternoon.AddDays(-7);
        var abandoned = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        DateTimeProvider.UtcNow = Afternoon;
        var today = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        today.ShouldNotBe(abandoned);
        (await CountVisitsAsync(ctx.MemberId)).ShouldBe(2);
    }

    [Fact]
    public async Task Being_mid_visit_at_one_branch_says_nothing_about_arriving_at_another()
    {
        // Branch is deliberately not a global query filter, and a member can genuinely train at two
        // sites in a day — so the guard is scoped, not gym-wide.
        var ctx = await SeedAsync();
        AsStaff(ctx);
        DateTimeProvider.UtcNow = Afternoon;

        var atMain = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));
        var atSecond = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.SecondBranchId, AttendanceMethod.QrSimulated));

        atSecond.ShouldNotBe(atMain);
        (await CountVisitsAsync(ctx.MemberId)).ShouldBe(2);
    }

    [Fact]
    public async Task Inside_is_judged_on_the_branchs_own_clock()
    {
        // 9pm Wednesday in New York is already Thursday in UTC. Read on a UTC calendar, the member
        // standing in the gym would look like yesterday's visitor and be given a second row.
        var ctx = await SeedAsync("America/New_York");
        AsStaff(ctx);

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 5, 21, 0, 0, TimeSpan.FromHours(-4));
        var first = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 5, 22, 0, 0, TimeSpan.FromHours(-4));
        var second = await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.QrSimulated));

        second.ShouldBe(first);
        (await CountVisitsAsync(ctx.MemberId)).ShouldBe(1);
    }

    private async Task<int> CountVisitsAsync(Guid memberId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.AttendanceRecords.IgnoreQueryFilters().CountAsync(a => a.MemberId == memberId);
    }

    private void AsStaff(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid SecondBranchId, Guid MemberId, Guid StaffUserId);

    private async Task<SeedContext> SeedAsync(string? timeZoneId = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US",
            TimeZone = timeZoneId ?? "UTC"
        };
        db.Branches.Add(branch);

        var second = new Branch
        {
            TenantId = tenant.Id, Name = "Riverside", AddressLine = "2 River St", City = "City", Country = "US",
            TimeZone = timeZoneId ?? "UTC"
        };
        db.Branches.Add(second);

        var staffUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(staffUser);
        // Both branches: BranchScopeBehavior refuses a command aimed at a branch the user cannot
        // reach, so the cross-branch test needs the access a real multi-site manager would have.
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branch.Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = second.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Aria",
            LastName = "First",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, second.Id, member.Id, staffUser.Id);
    }
}

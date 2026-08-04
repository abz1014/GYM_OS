using GymOS.Application.Modules.Reports.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Reports;

/// <summary>
/// The at-risk report shares its "how long is too long" threshold with ChurnRiskPolicy (the
/// automated win-back job) — these tests pin the report's inclusion rule against the same
/// FakeDateTimeProvider "today" the rest of the suite uses (2026-01-15).
/// </summary>
public class GetAtRiskMembersReportQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task An_active_member_who_visited_long_ago_is_flagged()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = NewMember(tenantId, branchId, "Ana", "Ríos", MemberStatus.Active);
        db.Members.Add(member);
        db.AttendanceRecords.Add(NewCheckIn(tenantId, branchId, member.Id, new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero)));
        await db.SaveChangesAsync();

        var result = await SendAsync(new GetAtRiskMembersReportQuery());

        result.ShouldHaveSingleItem();
        result[0].MemberId.ShouldBe(member.Id);
        result[0].DaysSinceLastVisit.ShouldBe(45); // Dec 1 -> Jan 15
    }

    [Fact]
    public async Task An_active_member_who_visited_recently_is_not_flagged()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = NewMember(tenantId, branchId, "Ben", "Osei", MemberStatus.Active);
        db.Members.Add(member);
        db.AttendanceRecords.Add(NewCheckIn(tenantId, branchId, member.Id, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero)));
        await db.SaveChangesAsync();

        (await SendAsync(new GetAtRiskMembersReportQuery())).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_member_who_has_never_visited_is_not_flagged()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.Members.Add(NewMember(tenantId, branchId, "Cai", "Nguyen", MemberStatus.Active));
        await db.SaveChangesAsync();

        (await SendAsync(new GetAtRiskMembersReportQuery())).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_frozen_member_who_visited_long_ago_is_not_flagged()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = NewMember(tenantId, branchId, "Dee", "Park", MemberStatus.Frozen);
        db.Members.Add(member);
        db.AttendanceRecords.Add(NewCheckIn(tenantId, branchId, member.Id, new DateTimeOffset(2025, 11, 1, 9, 0, 0, TimeSpan.Zero)));
        await db.SaveChangesAsync();

        (await SendAsync(new GetAtRiskMembersReportQuery())).ShouldBeEmpty();
    }

    private static Member NewMember(Guid tenantId, Guid branchId, string first, string last, MemberStatus status) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
        FirstName = first,
        LastName = last,
        Email = $"{Guid.NewGuid():N}@example.com",
        JoinDate = new DateOnly(2025, 1, 1),
        QrCodeToken = Guid.NewGuid().ToString("N"),
        Status = status
    };

    private static AttendanceRecord NewCheckIn(Guid tenantId, Guid branchId, Guid memberId, DateTimeOffset checkInAt) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberId = memberId,
        CheckInAt = checkInAt,
        Method = AttendanceMethod.Manual
    };

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId)> SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var staffUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(staffUser);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branch.Id });

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id);
    }
}

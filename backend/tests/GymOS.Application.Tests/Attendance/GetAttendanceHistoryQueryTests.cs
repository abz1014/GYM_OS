using GymOS.Application.Modules.Attendance.Queries;
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
/// CheckedInOnly and SearchTerm back the redesigned AttendancePage — front desk needs "who's here
/// right now" (no CheckOutAt) filterable by name, not just a plain paginated dump. These tests lock
/// down both filters and their combination.
/// </summary>
public class GetAttendanceHistoryQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task CheckedInOnly_excludes_members_who_have_already_checked_out()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;

        var stillIn = await AddRecordAsync(ctx.TenantId, ctx.BranchId, ctx.Member1Id, checkedOut: false);
        await AddRecordAsync(ctx.TenantId, ctx.BranchId, ctx.Member2Id, checkedOut: true);

        var result = await SendAsync(new GetAttendanceHistoryQuery(
            MemberId: null, BranchId: ctx.BranchId, FromDate: null, ToDate: null, SearchTerm: null, CheckedInOnly: true));

        result.Items.ShouldHaveSingleItem();
        result.Items.Single().Id.ShouldBe(stillIn);
    }

    [Fact]
    public async Task SearchTerm_matches_by_first_name_and_excludes_other_members()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;

        await AddRecordAsync(ctx.TenantId, ctx.BranchId, ctx.Member1Id, checkedOut: false);
        await AddRecordAsync(ctx.TenantId, ctx.BranchId, ctx.Member2Id, checkedOut: false);

        var result = await SendAsync(new GetAttendanceHistoryQuery(
            MemberId: null, BranchId: ctx.BranchId, FromDate: null, ToDate: null, SearchTerm: "Aria", CheckedInOnly: null));

        result.Items.ShouldHaveSingleItem();
        result.Items.Single().MemberName.ShouldBe("Aria First");
    }

    [Fact]
    public async Task CheckedInOnly_and_SearchTerm_combine_as_an_and_condition()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;

        // Aria's only visit is already checked out, so a name match alone isn't enough —
        // CheckedInOnly must still exclude it.
        await AddRecordAsync(ctx.TenantId, ctx.BranchId, ctx.Member1Id, checkedOut: true);

        var result = await SendAsync(new GetAttendanceHistoryQuery(
            MemberId: null, BranchId: ctx.BranchId, FromDate: null, ToDate: null, SearchTerm: "Aria", CheckedInOnly: true));

        result.Items.ShouldBeEmpty();
    }

    private async Task<Guid> AddRecordAsync(Guid tenantId, Guid branchId, Guid memberId, bool checkedOut)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var record = new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberId = memberId,
            CheckInAt = DateTimeOffset.UtcNow.AddHours(-2),
            CheckOutAt = checkedOut ? DateTimeOffset.UtcNow.AddHours(-1) : null,
            Method = AttendanceMethod.Manual
        };
        db.AttendanceRecords.Add(record);

        await db.SaveChangesAsync();
        return record.Id;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid Member1Id, Guid Member2Id, Guid StaffUserId)> SeedAsync()
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

        var member1 = new Member
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
        db.Members.Add(member1);

        var member2 = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Belle",
            LastName = "Second",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member2);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member1.Id, member2.Id, staffUser.Id);
    }
}

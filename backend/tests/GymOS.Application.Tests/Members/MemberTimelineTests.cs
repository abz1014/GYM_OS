using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Members.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// The member timeline, and the one property that matters most about it: merging six modules into a
/// single endpoint must not hand anyone a fact they could not already read on its own screen.
///
/// A receptionist holds billing but not workouts; a trainer holds workouts but not billing. Both
/// reach the same URL. The chronology each gets back has to be theirs.
/// </summary>
public class MemberTimelineTests : ApplicationTestBase
{
    [Fact]
    public async Task It_merges_every_permitted_source_newest_first()
    {
        var s = await SeedAsync();
        ActAs(s.TenantId, s.UserId,
            PermissionCodes.Members.View, PermissionCodes.Attendance.View,
            PermissionCodes.Workouts.View, PermissionCodes.Billing.View);

        var timeline = await SendAsync(new GetMemberTimelineQuery(s.MemberId));

        timeline.Select(e => e.Kind).ShouldContain("Visit");
        timeline.Select(e => e.Kind).ShouldContain("Workout");
        timeline.Select(e => e.Kind).ShouldContain("Invoice");

        // The whole point of the screen: one order, not six lists.
        timeline.Select(e => e.At).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Staff_without_billing_view_get_a_timeline_with_no_money_in_it()
    {
        var s = await SeedAsync();
        // A Trainer's real grant: members, attendance, workouts — and no billing.
        ActAs(s.TenantId, s.UserId,
            PermissionCodes.Members.View, PermissionCodes.Attendance.View, PermissionCodes.Workouts.View);

        var timeline = await SendAsync(new GetMemberTimelineQuery(s.MemberId));

        timeline.ShouldNotBeEmpty();
        timeline.Select(e => e.Kind).ShouldContain("Workout");
        timeline.Select(e => e.Kind).ShouldNotContain("Invoice");
        timeline.Select(e => e.Kind).ShouldNotContain("Payment");
    }

    [Fact]
    public async Task Staff_without_workouts_view_get_a_timeline_with_no_training_in_it()
    {
        var s = await SeedAsync();
        // A Receptionist's real grant: members, attendance, billing — and no workouts.
        ActAs(s.TenantId, s.UserId,
            PermissionCodes.Members.View, PermissionCodes.Attendance.View, PermissionCodes.Billing.View);

        var timeline = await SendAsync(new GetMemberTimelineQuery(s.MemberId));

        timeline.Select(e => e.Kind).ShouldContain("Invoice");
        timeline.Select(e => e.Kind).ShouldNotContain("Workout");
    }

    [Fact]
    public async Task A_member_in_another_tenant_is_not_found_rather_than_empty()
    {
        var foreign = await SeedAsync();
        var mine = await SeedAsync();
        ActAs(mine.TenantId, mine.UserId, PermissionCodes.Members.View, PermissionCodes.Attendance.View);

        // Empty would confirm the id exists somewhere. NotFound says nothing either way.
        await Should.ThrowAsync<NotFoundException>(
            () => SendAsync(new GetMemberTimelineQuery(foreign.MemberId)));
    }

    private void ActAs(Guid tenantId, Guid userId, params string[] permissions)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
        CurrentUser.Permissions = permissions;
    }

    private async Task<(Guid TenantId, Guid UserId, Guid MemberId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Time",
            LastName = "Line",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2026, 1, 1),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberId = member.Id,
            CheckInAt = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero),
            Method = AttendanceMethod.Manual
        });

        db.WorkoutLogs.Add(new WorkoutLog
        {
            TenantId = tenant.Id,
            MemberId = member.Id,
            LoggedAt = new DateTimeOffset(2026, 3, 1, 18, 0, 0, TimeSpan.Zero)
        });

        db.MembershipPlans.Add(new MembershipPlan
        {
            TenantId = tenant.Id,
            Name = "Monthly",
            Type = MembershipPlanType.Monthly,
            Price = 40m,
            Currency = "USD",
            DurationDays = 30
        });

        db.Invoices.Add(new Invoice
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberId = member.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            IssueDate = new DateOnly(2026, 2, 1),
            DueDate = new DateOnly(2026, 2, 8),
            Status = InvoiceStatus.Paid,
            Subtotal = 40m,
            TotalAmount = 40m,
            Currency = "USD"
        });

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, member.Id);
    }
}

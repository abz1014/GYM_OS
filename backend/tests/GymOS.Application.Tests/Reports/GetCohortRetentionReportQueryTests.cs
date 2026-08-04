using GymOS.Application.Modules.Reports.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Reports;

/// <summary>
/// One cohort bucket (join month) must yield exactly one retention number: still-Active count over
/// cohort size. FakeDateTimeProvider's "today" is 2026-01-15, so with MonthsBack=2 the buckets are
/// Dec 2025 and Jan 2026.
/// </summary>
public class GetCohortRetentionReportQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task A_cohorts_retention_rate_is_still_active_over_cohort_size()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        // December 2025 cohort: 2 joined, 1 still Active, 1 Cancelled.
        db.Members.Add(NewMember(tenantId, branchId, new DateOnly(2025, 12, 5), MemberStatus.Active));
        db.Members.Add(NewMember(tenantId, branchId, new DateOnly(2025, 12, 20), MemberStatus.Cancelled));
        await db.SaveChangesAsync();

        var points = await SendAsync(new GetCohortRetentionReportQuery(MonthsBack: 2));

        points.Count.ShouldBe(2);
        var decCohort = points.Single(p => p.CohortMonth == "Dec 2025");
        decCohort.CohortSize.ShouldBe(2);
        decCohort.StillActiveCount.ShouldBe(1);
        decCohort.RetentionRatePercent.ShouldBe(50.0);

        var janCohort = points.Single(p => p.CohortMonth == "Jan 2026");
        janCohort.CohortSize.ShouldBe(0);
        janCohort.RetentionRatePercent.ShouldBe(0.0);
    }

    private static Member NewMember(Guid tenantId, Guid branchId, DateOnly joinDate, MemberStatus status) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
        FirstName = "Cohort",
        LastName = $"Member-{Guid.NewGuid():N}"[..8],
        Email = $"{Guid.NewGuid():N}@example.com",
        JoinDate = joinDate,
        QrCodeToken = Guid.NewGuid().ToString("N"),
        Status = status
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

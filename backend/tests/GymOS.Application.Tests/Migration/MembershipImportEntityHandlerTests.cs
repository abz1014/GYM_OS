using System.Text.Json;
using GymOS.Application.Modules.Migration.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Migration;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Migration;

/// <summary>
/// MembershipImportEntityHandler is the first of the 3 "resolves a reference to an already-existing
/// entity" handlers (see GetImportEntitySchemasQuery's doc comment) — it must find the member by
/// email and the plan by name rather than creating either, and must never call
/// RenewMembershipCommand (which would invoice the member a second time for a period already paid
/// for in the legacy system). These tests drive the full Validate -> Commit -> Rollback pipeline,
/// matching ValidateImportJobCommandHandlerTests' shape rather than calling the handler directly.
/// </summary>
public class MembershipImportEntityHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task A_valid_row_commits_a_membership_and_rollback_cancels_it()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["PlanName"] = "Standard Monthly",
            ["StartDate"] = "2026-01-01",
            ["EndDate"] = "2026-12-31",
            ["PricePaid"] = "150"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));
        await SendAsync(new CommitImportJobCommand(jobId, ctx.BranchId));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
            job.Status.ShouldBe(ImportStatus.Completed);
            job.ValidRows.ShouldBe(1);

            var membership = await db.MemberMemberships.SingleAsync(m => m.MemberId == ctx.MemberId);
            membership.MembershipPlanId.ShouldBe(ctx.PlanId);
            membership.PricePaid.ShouldBe(150m);
            membership.Currency.ShouldBe("USD");
            membership.Status.ShouldBe(MemberMembershipStatus.Active);

            // No invoice was created for this import — a migrated membership was already billed
            // and paid for in the old system.
            (await db.Invoices.AnyAsync(i => i.MemberId == ctx.MemberId)).ShouldBeFalse();
        }

        await SendAsync(new RollbackImportJobCommand(jobId));

        using var scope2 = CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var rolledBack = await db2.MemberMemberships.SingleAsync(m => m.MemberId == ctx.MemberId);
        rolledBack.Status.ShouldBe(MemberMembershipStatus.Cancelled);
        rolledBack.CancellationReason.ShouldBe("Import rolled back");
    }

    [Fact]
    public async Task An_unknown_member_email_is_invalid_and_nothing_commits()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = "nobody@example.com",
            ["PlanName"] = "Standard Monthly",
            ["StartDate"] = "2026-01-01",
            ["EndDate"] = "2026-12-31",
            ["PricePaid"] = "150"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.ErrorRows.ShouldBe(1);
        job.ValidRows.ShouldBe(0);

        var row = await db.ImportRows.SingleAsync(r => r.ImportJobId == jobId);
        row.ValidationErrors.ShouldNotBeNull().ShouldContain("No member found");
    }

    [Fact]
    public async Task A_second_row_for_the_same_member_plan_and_start_date_is_a_duplicate()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.MemberMemberships.Add(new MemberMembership
            {
                MemberId = ctx.MemberId,
                MembershipPlanId = ctx.PlanId,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = MemberMembershipStatus.Active,
                PricePaid = 150m,
                Currency = "USD"
            });
            await db.SaveChangesAsync();
        }

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["PlanName"] = "Standard Monthly",
            ["StartDate"] = "2026-01-01",
            ["EndDate"] = "2026-12-31",
            ["PricePaid"] = "150"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope2 = CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = await db2.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.DuplicateRows.ShouldBe(1);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> SeedJobAsync(Guid tenantId, Dictionary<string, string> row)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var job = new ImportJob
        {
            TenantId = tenantId,
            EntityType = ImportEntityType.Membership,
            FileName = "memberships.csv",
            FileUrl = "local://unused",
            Status = ImportStatus.Uploaded,
            TotalRows = 1
        };
        db.ImportJobs.Add(job);

        foreach (var field in row.Keys)
        {
            db.ImportFieldMappings.Add(new ImportFieldMapping { ImportJobId = job.Id, SourceColumnName = field, TargetFieldName = field });
        }

        db.ImportRows.Add(new ImportRow
        {
            ImportJobId = job.Id,
            RowNumber = 1,
            RawDataJson = JsonSerializer.Serialize(row),
            Status = ImportRowStatus.Pending
        });

        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, string MemberEmail, Guid PlanId, Guid StaffUserId)> SeedAsync()
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

        var memberEmail = $"{Guid.NewGuid():N}@example.com";
        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = memberEmail,
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var plan = new MembershipPlan
        {
            TenantId = tenant.Id,
            Name = "Standard Monthly",
            Type = MembershipPlanType.Monthly,
            DurationDays = 30,
            Price = 150m,
            Currency = "USD",
            MaxFreezeDays = 14
        };
        db.MembershipPlans.Add(plan);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, memberEmail, plan.Id, staffUser.Id);
    }
}

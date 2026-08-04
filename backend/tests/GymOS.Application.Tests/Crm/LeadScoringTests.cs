using GymOS.Application.Modules.Crm.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Crm;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Crm;

/// <summary>
/// The lead list/detail queries must feed LeadScorePolicy real activity history rather than a
/// stubbed shortcut — these tests go through the actual queries against a real (SQLite) database
/// so a broken join or an off-by-one in the recency calculation shows up here, not just in the
/// pure policy tests.
/// </summary>
public class LeadScoringTests : ApplicationTestBase
{
    [Fact]
    public async Task An_untouched_leads_score_matches_the_policy_with_no_activity()
    {
        var (tenantId, branchId, userId, leadId) = await SeedLeadAsync(LeadStage.Lead, LeadSource.Website);
        SetAuthenticatedAs(tenantId, userId);

        var list = await SendAsync(new GetLeadsListQuery(Stage: null, BranchId: branchId));
        var detail = await SendAsync(new GetLeadByIdQuery(leadId));

        var expected = LeadScorePolicy.CalculateScore(LeadStage.Lead, LeadSource.Website, activityCount: 0, daysSinceLastActivity: null);
        list.Items.Single(l => l.Id == leadId).Score.ShouldBe(expected);
        detail.Score.ShouldBe(expected);
    }

    [Fact]
    public async Task A_leads_score_reflects_its_logged_activity_count_and_recency()
    {
        var (tenantId, branchId, userId, leadId) = await SeedLeadAsync(LeadStage.FollowUp, LeadSource.Referral);
        SetAuthenticatedAs(tenantId, userId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = leadId,
                Type = LeadActivityType.Call,
                Notes = "Called to discuss trial options.",
                CreatedAt = DateTimeProvider.UtcNow // "today" -> 0 days since -> <=3 recency band
            });
            await db.SaveChangesAsync();
        }

        var detail = await SendAsync(new GetLeadByIdQuery(leadId));

        var expected = LeadScorePolicy.CalculateScore(LeadStage.FollowUp, LeadSource.Referral, activityCount: 1, daysSinceLastActivity: 0);
        detail.Score.ShouldBe(expected);
        detail.Activities.ShouldHaveSingleItem();
        detail.Activities[0].CreatedAt.ShouldBe(DateTimeProvider.UtcNow);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId, Guid LeadId)> SeedLeadAsync(LeadStage stage, LeadSource source)
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

        var lead = new Lead
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            FirstName = "Cam",
            LastName = "Ortiz",
            Email = $"{Guid.NewGuid():N}@example.com",
            Source = source,
            Stage = stage
        };
        db.Leads.Add(lead);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id, lead.Id);
    }
}

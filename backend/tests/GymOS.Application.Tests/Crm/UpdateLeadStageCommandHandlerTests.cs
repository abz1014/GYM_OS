using GymOS.Application.Modules.Crm.Commands;
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
/// UpdateLeadStageCommand's core business rule: moving a lead to the Member stage must produce a
/// real Member record (via a nested CreateMemberCommand), not just relabel the lead — otherwise the
/// pipeline's "converted" count is disconnected from reality and nothing downstream ever sees them.
/// </summary>
public class UpdateLeadStageCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Moving_a_lead_to_Member_stage_creates_a_real_member_record()
    {
        var (tenantId, branchId, userId, leadId) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        await SendAsync(new UpdateLeadStageCommand(leadId, LeadStage.Member, ConvertedMemberId: null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var lead = await db.Leads.SingleAsync(l => l.Id == leadId);
        lead.Stage.ShouldBe(LeadStage.Member);
        lead.ConvertedMemberId.ShouldNotBeNull();

        var member = await db.Members.SingleAsync(m => m.Id == lead.ConvertedMemberId);
        member.Email.ShouldBe(lead.Email);
        member.BranchId.ShouldBe(branchId);
    }

    [Fact]
    public async Task Moving_a_lead_to_a_non_member_stage_never_creates_a_member()
    {
        var (tenantId, _, userId, leadId) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        await SendAsync(new UpdateLeadStageCommand(leadId, LeadStage.Trial, ConvertedMemberId: null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var lead = await db.Leads.SingleAsync(l => l.Id == leadId);
        lead.Stage.ShouldBe(LeadStage.Trial);
        lead.ConvertedMemberId.ShouldBeNull();
        (await db.Members.AnyAsync()).ShouldBeFalse();
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId, Guid LeadId)> SeedAsync()
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
            FirstName = "Jamie",
            LastName = "Fox",
            Email = $"{Guid.NewGuid():N}@example.com",
            Source = LeadSource.WalkIn,
            Stage = LeadStage.Trial
        };
        db.Leads.Add(lead);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id, lead.Id);
    }
}

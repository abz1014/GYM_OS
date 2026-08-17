using GymOS.Application.Modules.Coaching.Queries;
using GymOS.Application.Modules.Members.Queries;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Trainers;

/// <summary>
/// AssignClientCommand — the handler behind the "Assign Client" button on a trainer's page — shipped
/// without stamping TenantId on the TrainerAssignment it creates. TrainerAssignment is tenant-scoped
/// and the global query filter fails closed, so every assignment made through the real product was
/// silently unreachable the instant it was saved: invisible on the member's own profile, invisible on
/// the trainer's own client roster, and unreachable by the "end assignment" command that would have
/// let anyone notice and fix it by hand. Found live — a member showed no coach on their profile despite
/// staff having just used the assign screen, and the assignment could not be located through the app at
/// all, only by reading the row directly out of the database. These tests exercise the fix through the
/// two read paths that were actually broken, not just the write.
/// </summary>
public class AssignClientCommandTests : ApplicationTestBase
{
    [Fact]
    public async Task Assigning_a_client_is_visible_on_both_the_trainers_roster_and_the_members_profile()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "New");
        AsStaff(ctx);

        await SendAsync(new AssignClientCommand(ctx.TrainerId, memberId));

        // The member's own profile — GetMemberByIdQuery's coach lookup.
        var member = await SendAsync(new GetMemberByIdQuery(memberId));
        member.AssignedTrainerId.ShouldBe(ctx.TrainerId);

        // The trainer's own roster — GetMyClientsQuery. Signed in as the trainer, not staff.
        CurrentUser.UserId = ctx.TrainerUserId;
        var clients = await SendAsync(new GetMyClientsQuery());
        clients.Select(c => c.MemberId).ShouldContain(memberId);
    }

    [Fact]
    public async Task The_assignment_row_is_stamped_with_the_callers_tenant_not_left_empty()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Stamped");
        AsStaff(ctx);

        var assignmentId = await SendAsync(new AssignClientCommand(ctx.TrainerId, memberId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var assignment = await db.TrainerAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == assignmentId);

        assignment.TenantId.ShouldBe(ctx.TenantId);
        assignment.TenantId.ShouldNotBe(Guid.Empty);
    }

    private void AsStaff(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.OwnerUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> AddMemberAsync(SeedContext ctx, string firstName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = new Member
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = firstName,
            LastName = "Client",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2026, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N"),
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N"), IsActive = true };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var ownerUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Owner",
            LastName = "User",
        };
        var trainerUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Coach",
            LastName = "User",
        };
        db.Users.AddRange(ownerUser, trainerUser);
        await db.SaveChangesAsync();

        var trainer = new Trainer { TenantId = tenant.Id, BranchId = branch.Id, UserId = trainerUser.Id };
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync();

        return new SeedContext(tenant.Id, branch.Id, trainer.Id, ownerUser.Id, trainerUser.Id);
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid TrainerId, Guid OwnerUserId, Guid TrainerUserId);
}

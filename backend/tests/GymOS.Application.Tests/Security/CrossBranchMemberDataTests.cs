using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Modules.Nutrition.Queries;
using GymOS.Application.Modules.Workouts.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Security;

/// <summary>
/// A member who is invisible must not have visible belongings.
///
/// THE DEFECT THESE PIN, found by reading a live database rather than the source: a trainer with
/// branch access to Downtown only asked the API for an Uptown member and got
///
///   GET /api/members/{id}               -> 404   (the member was correctly hidden)
///   GET /api/workouts/logs/member/{id}  -> 200   (seven complete sessions, with loads)
///
/// Branch isolation is enforced by EF global query filters, and a filter attaches to an entity only
/// if that entity carries the column being filtered on. Member is IBranchScoped; WorkoutLog,
/// WorkoutAssignment, DietPlan and WaterLog are tenant-scoped but carry NO BranchId. So four handlers
/// written as `.Where(x => x.MemberId == request.MemberId)` were filtered by the caller's own route
/// parameter and by nothing else.
///
/// The fix routes every such read through MemberScope, which asks the already-filtered Members set
/// whether the caller may see this member at all. These tests exist because the next handler keyed
/// on a member id will be written by someone who has never heard of this bug.
/// </summary>
public class CrossBranchMemberDataTests : ApplicationTestBase
{
    [Fact]
    public async Task Workout_logs_of_a_member_in_another_branch_are_not_readable()
    {
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberWorkoutLogsQuery(w.ForeignMemberId)));
    }

    [Fact]
    public async Task Workout_assignments_of_a_member_in_another_branch_are_not_readable()
    {
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberWorkoutAssignmentsQuery(w.ForeignMemberId)));
    }

    [Fact]
    public async Task Diet_plans_of_a_member_in_another_branch_are_not_readable()
    {
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberDietPlansQuery(w.ForeignMemberId)));
    }

    [Fact]
    public async Task Water_logs_of_a_member_in_another_branch_are_not_readable()
    {
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberWaterLogsQuery(w.ForeignMemberId)));
    }

    [Fact]
    public async Task A_member_in_my_own_branch_reads_exactly_as_before()
    {
        /*
         * The negative control, and the one that matters most: the failure mode of a guard like this
         * is locking staff out of the members they exist to serve. A test file that only proves
         * things are refused would pass just as happily if the guard refused everyone.
         *
         * Diet plans rather than workout logs, and that is a harness limit rather than a preference:
         * GetMemberWorkoutLogsQuery orders by a DateTimeOffset, which SQLite cannot translate in an
         * ORDER BY — the same constraint the rest of this codebase reduces in memory for. The four
         * refusal tests above still exercise the workout paths, because the guard throws before the
         * ordering is ever reached.
         */
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        var plans = await SendAsync(new GetMemberDietPlansQuery(w.HomeMemberId));

        plans.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task An_invisible_member_is_indistinguishable_from_one_that_does_not_exist()
    {
        /*
         * NOT FOUND, not FORBIDDEN. "Forbidden" would confirm the id names a real member, turning the
         * endpoint into an oracle for enumerating another branch's membership one guess at a time.
         * A nonexistent id and a hidden id must answer identically.
         */
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        var hidden = await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberWorkoutLogsQuery(w.ForeignMemberId)));
        var absent = await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new GetMemberWorkoutLogsQuery(Guid.NewGuid())));

        hidden.GetType().ShouldBe(absent.GetType());
    }

    [Fact]
    public async Task The_staff_water_log_refuses_an_amount_that_would_break_the_report()
    {
        /*
         * Asymmetric validation, and the reason a report went down. LogMyWaterCommand (the member's
         * own portal) capped at 5000ml; this command — which the portal DELEGATES INTO, and which
         * staff call directly — only required "greater than zero". A single int.MaxValue row
         * overflowed GetNutritionReportQuery's sum and returned 500 for every role, tenant-wide,
         * until it was deleted by hand.
         */
        var w = await SeedTwoBranchesAsync();
        SignInAt(w.HomeBranchId, w.TenantId, w.StaffUserId);

        await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new LogWaterCommand(w.HomeMemberId, int.MaxValue)));

        await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new LogWaterCommand(w.HomeMemberId, LogWaterCommandValidator.MaxAmountMl + 1)));

        // The bound is inclusive, and an ordinary drink still logs.
        await SendAsync(new LogWaterCommand(w.HomeMemberId, LogWaterCommandValidator.MaxAmountMl));
        await SendAsync(new LogWaterCommand(w.HomeMemberId, 500));
    }

    // ---- harness ----

    private void SignInAt(Guid branchId, Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
        // AccessibleBranchIds is what the branch filter reads. Left null it means "system context,
        // every branch visible" — which would make these tests pass for the wrong reason.
        CurrentUser.AccessibleBranchIds = [branchId];
        CurrentUser.BranchId = branchId;
    }

    private record World(Guid TenantId, Guid HomeBranchId, Guid StaffUserId, Guid HomeMemberId, Guid ForeignMemberId);

    private async Task<World> SeedTwoBranchesAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"T-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        Branch NewBranch(string name) =>
            new() { TenantId = tenant.Id, Name = name, AddressLine = "1 St", City = "C", Country = "US" };

        var home = NewBranch("Downtown");
        var foreign = NewBranch("Uptown");
        db.Branches.AddRange(home, foreign);

        var staff = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused", FirstName = "Staff", LastName = "User"
        };
        db.Users.Add(staff);

        Member NewMember(Branch branch, string first) => new()
        {
            TenantId = tenant.Id, BranchId = branch.Id,
            MemberCode = $"M-{Guid.NewGuid():N}"[..10],
            FirstName = first, LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active, QrCodeToken = Guid.NewGuid().ToString("N")
        };

        var mine = NewMember(home, "Mine");
        var theirs = NewMember(foreign, "Theirs");
        db.Members.AddRange(mine, theirs);

        // Both members carry the same shapes of data, so a passing test cannot be an artefact of the
        // foreign member simply having nothing to leak.
        foreach (var m in new[] { mine, theirs })
        {
            db.WorkoutLogs.Add(new WorkoutLog
            {
                TenantId = tenant.Id, MemberId = m.Id, LoggedAt = DateTimeOffset.UtcNow
            });
            db.DietPlans.Add(new DietPlan
            {
                TenantId = tenant.Id, MemberId = m.Id, Name = "Plan",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.WaterLogs.Add(new WaterLog
            {
                TenantId = tenant.Id, MemberId = m.Id, AmountMl = 500, LoggedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        return new World(tenant.Id, home.Id, staff.Id, mine.Id, theirs.Id);
    }
}

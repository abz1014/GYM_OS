using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// Zero-logging: the app proposes, the member confirms, and a mis-tap can be taken back.
///
/// The proposal must work for a member with no trainer (most of a gym), must never invent a load,
/// and undo must leave nothing behind — a phantom personal record or orphaned XP would corrupt
/// exactly the numbers that make logging worth doing.
///
/// Clock fixed to Thursday 2026-08-06.
/// </summary>
public class ZeroLoggingTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Thursday = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    public ZeroLoggingTests() => DateTimeProvider.UtcNow = Thursday;

    [Fact]
    public async Task A_member_with_history_is_offered_their_last_session()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 140m)]));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe("RepeatLast");
        proposal.CanConfirm.ShouldBeTrue();
        var entry = proposal.Entries.ShouldHaveSingleItem();
        entry.ExerciseName.ShouldBe("Deadlift");
        entry.Sets.ShouldBe(3);
        entry.Reps.ShouldBe(8);
        entry.WeightKg.ShouldBe(140m);
    }

    [Fact]
    public async Task The_most_recent_session_wins_not_the_first()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        DateTimeProvider.UtcNow = Thursday.AddDays(-5);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 5, 5, 100m)]));
        DateTimeProvider.UtcNow = Thursday;
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.BenchId, 3, 10, 60m)]));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Entries.ShouldHaveSingleItem().ExerciseName.ShouldBe("Bench Press");
    }

    [Fact]
    public async Task A_brand_new_member_still_gets_something_to_confirm()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe("Starter");
        proposal.CanConfirm.ShouldBeTrue();
        proposal.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_trainer_plan_takes_precedence_and_borrows_remembered_weights()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        // History gives the load; the plan gives the prescription.
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.BenchId, 3, 8, 65m)]));
        await AssignPlanAsync(ctx, (ctx.BenchId, 4, 12));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe("TrainerPlan");
        var entry = proposal.Entries.ShouldHaveSingleItem();
        entry.Sets.ShouldBe(4);        // from the plan
        entry.Reps.ShouldBe(12);       // from the plan
        entry.WeightKg.ShouldBe(65m);  // from what they actually lifted
    }

    [Fact]
    public async Task An_expired_plan_falls_back_to_the_last_session()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 120m)]));
        await AssignPlanAsync(ctx, (ctx.BenchId, 4, 12), endedDaysAgo: 3);

        (await SendAsync(new GetMyNextSessionQuery())).Source.ShouldBe("RepeatLast");
    }

    [Fact]
    public async Task A_plan_is_offered_in_the_order_the_trainer_wrote_it()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        // Deadlift is written first but added second, so passing requires reading OrderIndex rather
        // than whatever order the rows happen to come back in. Order is the prescription: a squat
        // belongs before the accessory work, not after it.
        await AssignPlanAsync(ctx, "Leg Day", startedDaysAgo: 1, (ctx.BenchId, 3, 8, 1), (ctx.DeadliftId, 5, 5, 0));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe("TrainerPlan");
        proposal.Entries.Select(e => e.ExerciseName).ShouldBe(["Deadlift", "Bench Press"]);
    }

    [Fact]
    public async Task Only_the_newest_active_plan_is_offered_never_a_merge_of_several()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        // A member moved onto a new block before the old one lapses has two active assignments.
        await AssignPlanAsync(ctx, "Old block", startedDaysAgo: 30, (ctx.BenchId, 3, 8, 0));
        await AssignPlanAsync(ctx, "New block", startedDaysAgo: 1, (ctx.DeadliftId, 5, 5, 0));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        // Flattening both would hand the member a session no trainer ever prescribed — and any
        // exercise appearing in both plans would be logged twice on a single confirm.
        proposal.Entries.ShouldHaveSingleItem().ExerciseName.ShouldBe("Deadlift");
    }

    [Fact]
    public async Task Confirming_a_proposal_goes_through_the_same_path_as_typing_it()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 140m)]));

        var proposal = await SendAsync(new GetMyNextSessionQuery());
        var result = await SendAsync(new LogMyWorkoutCommand(
            proposal.Entries.Select(e => new WorkoutLogEntryInput(e.ExerciseId, e.Sets, e.Reps, e.WeightKg)).ToList()));

        // The full cascade fired — this is the whole reason confirmation reuses the log command.
        result.XpEarned.ShouldBeGreaterThan(0);
        result.SessionsThisWeek.ShouldBe(1);
    }

    [Fact]
    public async Task Undo_removes_the_session_and_the_xp_it_earned()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 140m)]));

        await SendAsync(new UndoMyWorkoutCommand(result.WorkoutLogId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.WorkoutLogs.AnyAsync(w => w.Id == result.WorkoutLogId)).ShouldBeFalse();
        (await db.XpTransactions.AnyAsync(t => t.SourceId == result.WorkoutLogId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Undo_removes_a_personal_record_that_never_really_happened()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 200m)]));

        using (var before = CreateScope())
        {
            var db = before.ServiceProvider.GetRequiredService<GymOsDbContext>();
            (await db.PersonalRecords.AnyAsync(r => r.WorkoutLogId == result.WorkoutLogId)).ShouldBeTrue();
        }

        await SendAsync(new UndoMyWorkoutCommand(result.WorkoutLogId));

        using var scope = CreateScope();
        var after = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await after.PersonalRecords.AnyAsync(r => r.WorkoutLogId == result.WorkoutLogId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Undo_walks_the_level_back_to_what_the_remaining_ledger_says()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 100m)]));
        var second = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.BenchId, 3, 8, 50m)]));

        long TotalXpFor(GymOsDbContext db) =>
            db.XpTransactions.Where(t => t.MemberId == ctx.MemberId).Sum(t => t.Amount);

        await SendAsync(new UndoMyWorkoutCommand(second.WorkoutLogId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var progression = await db.MemberProgressions.SingleAsync(p => p.MemberId == ctx.MemberId);
        progression.TotalXp.ShouldBe(TotalXpFor(db));   // projection matches the ledger that's left
    }

    [Fact]
    public async Task Undo_leaves_the_members_other_sessions_alone()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var keep = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 100m)]));
        var drop = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.BenchId, 3, 8, 50m)]));

        await SendAsync(new UndoMyWorkoutCommand(drop.WorkoutLogId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.WorkoutLogs.AnyAsync(w => w.Id == keep.WorkoutLogId)).ShouldBeTrue();
        (await db.XpTransactions.AnyAsync(t => t.SourceId == keep.WorkoutLogId)).ShouldBeTrue();
    }

    [Fact]
    public async Task An_old_session_can_no_longer_be_undone()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 140m)]));

        // Past the window: undo is for a mis-tap noticed now, not for rewriting history.
        DateTimeProvider.UtcNow = Thursday.Add(WorkoutUndoPolicy.UndoWindow).AddMinutes(1);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => SendAsync(new UndoMyWorkoutCommand(result.WorkoutLogId)));
    }

    [Fact]
    public async Task I_cannot_undo_someone_elses_session()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();

        AsMember(theirs);
        var theirLog = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(theirs.DeadliftId, 3, 8, 100m)]));

        AsMember(mine);
        // 404 rather than 403 — their log's existence is never confirmed.
        await Should.ThrowAsync<NotFoundException>(() => SendAsync(new UndoMyWorkoutCommand(theirLog.WorkoutLogId)));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.WorkoutLogs.AnyAsync(w => w.Id == theirLog.WorkoutLogId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Undoing_the_only_session_this_week_reopens_the_ring()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.DeadliftId, 3, 8, 140m)]));
        (await SendAsync(new GetMyTodayQuery())).SessionsThisWeek.ShouldBe(1);

        await SendAsync(new UndoMyWorkoutCommand(result.WorkoutLogId));

        (await SendAsync(new GetMyTodayQuery())).SessionsThisWeek.ShouldBe(0);
    }

    private void AsMember(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task AssignPlanAsync(SeedContext ctx, (Guid ExerciseId, int Sets, int Reps) exercise, int? endedDaysAgo = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var today = DateOnly.FromDateTime(Thursday.UtcDateTime);

        var template = new WorkoutTemplate { TenantId = ctx.TenantId, Name = "Push Day" };
        template.TemplateExercises.Add(new WorkoutTemplateExercise
        {
            ExerciseId = exercise.ExerciseId, SetsCount = exercise.Sets, RepsCount = exercise.Reps, OrderIndex = 0
        });
        db.WorkoutTemplates.Add(template);

        db.WorkoutAssignments.Add(new WorkoutAssignment
        {
            MemberId = ctx.MemberId,
            WorkoutTemplateId = template.Id,
            StartDate = today.AddDays(-30),
            EndDate = endedDaysAgo is int ended ? today.AddDays(-ended) : null
        });

        await db.SaveChangesAsync();
    }

    private async Task AssignPlanAsync(
        SeedContext ctx, string name, int startedDaysAgo, params (Guid ExerciseId, int Sets, int Reps, int OrderIndex)[] exercises)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var template = new WorkoutTemplate { TenantId = ctx.TenantId, Name = name };
        foreach (var (exerciseId, sets, reps, order) in exercises)
        {
            template.TemplateExercises.Add(new WorkoutTemplateExercise
            {
                ExerciseId = exerciseId, SetsCount = sets, RepsCount = reps, OrderIndex = order
            });
        }

        db.WorkoutTemplates.Add(template);
        db.WorkoutAssignments.Add(new WorkoutAssignment
        {
            MemberId = ctx.MemberId,
            WorkoutTemplateId = template.Id,
            StartDate = DateOnly.FromDateTime(Thursday.UtcDateTime).AddDays(-startedDaysAgo)
        });

        await db.SaveChangesAsync();
    }

    private record SeedContext(Guid TenantId, Guid MemberId, Guid DeadliftId, Guid BenchId, Guid MemberUserId);

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Member", LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var deadlift = new Exercise { TenantId = tenant.Id, Name = "Deadlift", MuscleGroup = "Back", Equipment = "Barbell" };
        var bench = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.AddRange(deadlift, bench);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, member.Id, deadlift.Id, bench.Id, user.Id);
    }
}

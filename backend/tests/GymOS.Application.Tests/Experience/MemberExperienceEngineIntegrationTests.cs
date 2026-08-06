using GymOS.Application.Modules.Attendance.Commands;
using GymOS.Application.Modules.Challenges.Commands;
using GymOS.Application.Modules.Challenges.Queries;
using GymOS.Application.Modules.Coaching.Queries;
using GymOS.Application.Modules.Engagement.Queries;
using GymOS.Application.Modules.Experience.Commands;
using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Experience;

/// <summary>
/// Cross-slice integration: all ten MEE slices exercised together against ONE member through the
/// real MediatR pipeline, rather than each slice verified in isolation.
///
/// This is the test the per-slice suites structurally cannot be: every slice from S2 onward hangs
/// off the same WorkoutLoggedEvent/MemberProgressionChangedEvent fan-out, so the interesting
/// failures are interaction failures — two handlers racing on the same unsaved row, a projection
/// that disagrees with the ledger it's derived from, a dashboard that counts a member the portal
/// says is someone else. (Both real bugs found during S8 and S10 were exactly this shape.)
/// </summary>
public class MemberExperienceEngineIntegrationTests : ApplicationTestBase
{
    [Fact]
    public async Task One_members_real_activity_cascades_coherently_across_all_ten_slices()
    {
        var ctx = await SeedAsync();
        var today = DateTimeProvider.UtcNow;

        // ---- S8: the member opts into a challenge they haven't yet cleared (target 2 workouts).
        AsMember(ctx);
        await SendAsync(new JoinChallengeCommand(ctx.ChallengeId));

        // ---- Real activity: two workouts (progressive: 60kg -> 65kg) plus a gym check-in.
        DateTimeProvider.UtcNow = today.AddDays(-1);
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        DateTimeProvider.UtcNow = today;
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 65m)]));
        await SendAsync(new CheckInCommand(ctx.MemberId, ctx.BranchId, AttendanceMethod.Manual));

        // ================= S1: XP ledger + level projection =================
        var experience = await SendAsync(new GetMyExperienceQuery());
        var expectedXp =
            (2 * XpPolicy.AwardFor(XpReason.WorkoutCompleted))     // two workouts
            + XpPolicy.AwardFor(XpReason.GymVisit)                 // one check-in
            + XpPolicy.AwardFor(XpReason.ChallengeCompleted);      // challenge cleared by workout #2
        experience.TotalXp.ShouldBe(expectedXp);
        experience.Level.ShouldBe(XpPolicy.LevelForXp(expectedXp).Level);
        experience.Recent.ShouldNotBeEmpty();

        // The projection must agree with the ledger it's derived from — the invariant S10 exists to restore.
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var ledgerSum = (await db.XpTransactions.Where(t => t.MemberId == ctx.MemberId)
                .Select(t => t.Amount).ToListAsync()).Sum(a => (long)a);
            ledgerSum.ShouldBe(experience.TotalXp);
        }

        // ================= S2: personal records + mastery =================
        var records = await SendAsync(new GetMyPersonalRecordsQuery());
        records.ShouldNotBeEmpty();
        records.ShouldAllBe(r => r.ExerciseId == ctx.ExerciseId);

        var mastery = await SendAsync(new GetMyMasteryQuery());
        mastery.MuscleGroups.ShouldContain(g => g.Name == "Chest");

        // ================= S3: achievements unlocked exactly once =================
        var achievements = await SendAsync(new GetMyAchievementsQuery());
        achievements.ShouldContain(a => a.Code == "first-workout" && a.Unlocked);
        achievements.ShouldContain(a => a.Code == "first-visit" && a.Unlocked);
        achievements.ShouldContain(a => a.Code == "first-pr" && a.Unlocked);
        achievements.ShouldContain(a => a.Code == "first-challenge" && a.Unlocked);
        achievements.Where(a => a.Unlocked).Select(a => a.Code).ShouldBeUnique();

        // ================= S4: streaks =================
        var streaks = await SendAsync(new GetMyStreaksQuery());
        streaks.WorkoutWeeks.ShouldBeGreaterThan(0);
        streaks.AttendanceWeeks.ShouldBeGreaterThan(0);

        // ================= S5: recovery reflects the real load, always explained =================
        var recovery = await SendAsync(new GetMyRecoveryQuery());
        recovery.SessionsLast7Days.ShouldBe(2);
        recovery.DaysSinceLastWorkout.ShouldBe(0);
        recovery.Reason.ShouldNotBeNullOrWhiteSpace();
        recovery.MuscleGroups.ShouldContain(m => m.MuscleGroup == "Chest");

        // ================= S6: recommendations, every one explained =================
        var recommendations = await SendAsync(new GetMyRecommendationsQuery());
        recommendations.ShouldAllBe(r => !string.IsNullOrWhiteSpace(r.Explanation));

        // ================= S7: timeline merges what the other slices produced =================
        var timeline = await SendAsync(new GetMyTimelineQuery());
        // The sessions are the spine, and the records they set are reported as part of them rather
        // than as separate lines — both workouts here set records, so neither stands alone.
        timeline.Count(e => e.Type == "Workout").ShouldBe(2);
        timeline.ShouldContain(e => e.Type == "Workout" && e.Description!.Contains("best"));
        timeline.ShouldNotContain(e => e.Type == "PersonalRecord");
        timeline.ShouldContain(e => e.Type == "Achievement");
        timeline.Select(e => e.OccurredAt).ShouldBe(timeline.Select(e => e.OccurredAt).OrderByDescending(o => o));

        // ================= S8: challenge completed by the same workouts =================
        var challenges = await SendAsync(new GetMyChallengesQuery());
        var challenge = challenges.ShouldHaveSingleItem();
        challenge.Joined.ShouldBeTrue();
        challenge.IsCompleted.ShouldBeTrue();
        challenge.MyWorkoutCount.ShouldBe(2);

        // ================= S9: staff dashboards see the SAME member coherently =================
        AsStaff(ctx);

        var compliance = await SendAsync(new GetCoachingComplianceQuery());
        var row = compliance.ShouldHaveSingleItem();
        row.MemberId.ShouldBe(ctx.MemberId);
        row.WorkoutAdherencePercent.ShouldBeGreaterThan(0);
        row.LastWorkoutAt.ShouldNotBeNull();

        var risks = await SendAsync(new GetCoachingRisksQuery());
        // Two sessions with a check-in this week is a healthy load — no risk flag either way.
        risks.ShouldNotContain(r => r.MemberId == ctx.MemberId && r.RiskType == "OvertrainingRisk");

        var engagement = await SendAsync(new GetEngagementSummaryQuery());
        engagement.TotalActiveMembers.ShouldBe(1);
        engagement.XpEarnedLast30Days.ShouldBe(expectedXp);      // manager view agrees with the member's own
        engagement.ChallengeParticipants.ShouldBe(1);
        engagement.ChallengeCompletions.ShouldBe(1);
        engagement.MembersWithActiveStreak.ShouldBe(1);
        engagement.LevelDistribution.Sum(l => l.MemberCount).ShouldBe(1);
        engagement.LevelDistribution.ShouldContain(l => l.Level == experience.Level);

        // ================= S10: rebuild is a no-op over a healthy system =================
        var rebuild = await SendAsync(new RebuildExperienceProjectionsCommand());
        rebuild.AchievementsBackfilled.ShouldBe(0); // the live pipeline already unlocked everything

        // Every member-facing read must be byte-identical after the rebuild.
        AsMember(ctx);
        var experienceAfter = await SendAsync(new GetMyExperienceQuery());
        experienceAfter.TotalXp.ShouldBe(experience.TotalXp);
        experienceAfter.Level.ShouldBe(experience.Level);

        var masteryAfter = await SendAsync(new GetMyMasteryQuery());
        masteryAfter.MuscleGroups.Select(g => (g.Name, g.MasteryPercent))
            .ShouldBe(mastery.MuscleGroups.Select(g => (g.Name, g.MasteryPercent)));

        var achievementsAfter = await SendAsync(new GetMyAchievementsQuery());
        achievementsAfter.Where(a => a.Unlocked).Select(a => a.Code).OrderBy(c => c)
            .ShouldBe(achievements.Where(a => a.Unlocked).Select(a => a.Code).OrderBy(c => c));

        var challengesAfter = await SendAsync(new GetMyChallengesQuery());
        challengesAfter.ShouldHaveSingleItem().IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Repeating_the_whole_cascade_never_double_awards()
    {
        // The idempotency guarantee that ties S1/S2/S3/S8 together: re-running the same activity
        // pattern must add new XP for the NEW events only, and must never re-unlock an achievement
        // or re-complete an already-completed challenge.
        var ctx = await SeedAsync();
        AsMember(ctx);
        var today = DateTimeProvider.UtcNow;

        await SendAsync(new JoinChallengeCommand(ctx.ChallengeId));
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));
        DateTimeProvider.UtcNow = today.AddDays(1);
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 65m)]));

        var afterCompletion = await SendAsync(new GetMyExperienceQuery());

        // Joining again and logging a third workout: only the new workout's XP may be added.
        await SendAsync(new JoinChallengeCommand(ctx.ChallengeId));
        DateTimeProvider.UtcNow = today.AddDays(2);
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 70m)]));

        var final = await SendAsync(new GetMyExperienceQuery());

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        // Challenge XP credited exactly once despite a second join and a further workout.
        (await db.XpTransactions.CountAsync(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.ChallengeCompleted))
            .ShouldBe(1);
        (await db.ChallengeParticipants.CountAsync(p => p.MemberId == ctx.MemberId)).ShouldBe(1);

        // Achievements are unique per code.
        var codes = await db.MemberAchievements.Where(a => a.MemberId == ctx.MemberId).Select(a => a.Code).ToListAsync();
        codes.ShouldBeUnique();

        // The only XP delta is the third workout itself.
        (final.TotalXp - afterCompletion.TotalXp).ShouldBe(XpPolicy.AwardFor(XpReason.WorkoutCompleted));
    }

    private void AsMember((Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid MemberUserId, Guid StaffUserId, Guid ChallengeId) ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private void AsStaff((Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid MemberUserId, Guid StaffUserId, Guid ChallengeId) ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid MemberUserId, Guid StaffUserId, Guid ChallengeId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var memberUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Member", LastName = "User"
        };
        var staffUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Staff", LastName = "User"
        };
        db.Users.AddRange(memberUser, staffUser);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = memberUser.Id, BranchId = branch.Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = memberUser.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N"), Status = MemberStatus.Active
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        var todayDate = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);
        var challenge = new CommunityChallenge
        {
            TenantId = tenant.Id, BranchId = null, Name = "Integration Challenge",
            StartDate = todayDate.AddDays(-7), EndDate = todayDate.AddDays(7), TargetWorkoutCount = 2
        };
        db.CommunityChallenges.Add(challenge);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, exercise.Id, memberUser.Id, staffUser.Id, challenge.Id);
    }
}

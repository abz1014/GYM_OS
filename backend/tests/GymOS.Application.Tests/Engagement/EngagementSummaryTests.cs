using GymOS.Application.Modules.Engagement.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Engagement;

/// <summary>
/// Slice 9: the manager engagement dashboard — XP velocity, active streaks, challenge participation,
/// level distribution, and the at-risk-vs-active retention correlation — proven for correctness and
/// for the branch isolation the design explicitly calls for (a report the existing tenant-wide
/// GetAtRiskMembersReportQuery does NOT itself apply, so this query has to add it back).
/// </summary>
public class EngagementSummaryTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Today = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summary_aggregates_xp_streaks_and_challenge_participation()
    {
        var ctx = await SeedTenantAsync();
        var engaged = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);
        var quiet = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = engaged, Amount = 50, Reason = XpReason.WorkoutCompleted,
                SourceType = XpSourceType.WorkoutLog, OccurredAt = Today.AddDays(-1)
            });
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = engaged, CheckInAt = Today
            });

            var challenge = new CommunityChallenge
            {
                TenantId = ctx.TenantId, BranchId = null, Name = "Test Challenge",
                StartDate = DateOnly.FromDateTime(Today.UtcDateTime).AddDays(-7),
                EndDate = DateOnly.FromDateTime(Today.UtcDateTime).AddDays(7), TargetWorkoutCount = 3
            };
            db.CommunityChallenges.Add(challenge);
            await db.SaveChangesAsync();

            db.ChallengeParticipants.Add(new ChallengeParticipant
            {
                ChallengeId = challenge.Id, MemberId = engaged, JoinedAt = Today.AddDays(-5),
                IsCompleted = true, CompletedAt = Today.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var summary = await SendAsync(new GetEngagementSummaryQuery());

        summary.TotalActiveMembers.ShouldBe(2);
        summary.XpEarnedLast30Days.ShouldBe(50);
        summary.MembersWithActiveStreak.ShouldBe(1);
        summary.ChallengeParticipants.ShouldBe(1);
        summary.ChallengeCompletions.ShouldBe(1);
        quiet.ShouldNotBe(Guid.Empty); // the quiet member exists only to prove it does NOT inflate the counts above
    }

    [Fact]
    public async Task Summary_excludes_members_outside_the_callers_accessible_branches()
    {
        var ctx = await SeedTenantAsync();
        var branchBMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchBId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = branchBMember, Amount = 999, Reason = XpReason.WorkoutCompleted,
                SourceType = XpSourceType.WorkoutLog, OccurredAt = Today.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId); // access to Branch A only
        DateTimeProvider.UtcNow = Today;

        var summary = await SendAsync(new GetEngagementSummaryQuery());

        summary.TotalActiveMembers.ShouldBe(0);
        summary.XpEarnedLast30Days.ShouldBe(0);
    }

    [Fact]
    public async Task Summary_level_distribution_defaults_absent_members_to_level_1()
    {
        var ctx = await SeedTenantAsync();
        var leveledMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);
        var freshMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId); // never earned any XP

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var progression = new MemberProgression { TenantId = ctx.TenantId, MemberId = leveledMember, UpdatedAt = Today };
            progression.SetTotalXp(1000); // exactly the cumulative XP for level 5
            db.MemberProgressions.Add(progression);
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var summary = await SendAsync(new GetEngagementSummaryQuery());

        summary.LevelDistribution.ShouldContain(r => r.Level == 5 && r.MemberCount == 1);
        summary.LevelDistribution.ShouldContain(r => r.Level == 1 && r.MemberCount == 1); // freshMember, no row at all
        freshMember.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Summary_retention_correlation_separates_at_risk_members_by_average_level()
    {
        var ctx = await SeedTenantAsync();
        var atRiskMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);
        var activeMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            // At-risk: Active status, but hasn't checked in for 20 days (over ChurnRiskPolicy's 14-day threshold).
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = atRiskMember, CheckInAt = Today.AddDays(-20)
            });

            // Active: checked in today, and levelled up.
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = activeMember, CheckInAt = Today
            });
            var progression = new MemberProgression { TenantId = ctx.TenantId, MemberId = activeMember, UpdatedAt = Today };
            progression.SetTotalXp(1000); // level 5
            db.MemberProgressions.Add(progression);

            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var summary = await SendAsync(new GetEngagementSummaryQuery());

        summary.Retention.AtRiskMemberCount.ShouldBe(1);
        summary.Retention.AtRiskAverageLevel.ShouldBe(1); // never earned XP -> default level 1
        summary.Retention.ActiveMemberCount.ShouldBe(1);
        summary.Retention.ActiveAverageLevel.ShouldBe(5);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> SeedMemberAsync(Guid tenantId, Guid branchId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var user = new User
        {
            TenantId = tenantId, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Test", LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenantId, BranchId = branchId, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<(Guid TenantId, Guid BranchAId, Guid BranchBId, Guid StaffUserId)> SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branchA = new Branch { TenantId = tenant.Id, Name = "Branch A", AddressLine = "1 Main St", City = "City", Country = "US" };
        var branchB = new Branch { TenantId = tenant.Id, Name = "Branch B", AddressLine = "2 Main St", City = "City", Country = "US" };
        db.Branches.AddRange(branchA, branchB);

        var staffUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Manager", LastName = "User"
        };
        db.Users.Add(staffUser);

        await db.SaveChangesAsync();

        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branchA.Id });
        await db.SaveChangesAsync();

        return (tenant.Id, branchA.Id, branchB.Id, staffUser.Id);
    }
}

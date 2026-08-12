using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The rank screen's second read: the ladder, and the race the member is actually in.
///
/// The gym-wide leaderboard puts a Newcomer ninetieth behind eight Titans, which is a fact and not a
/// contest. These tests pin the two things that make the rung board different — it contains ONLY the
/// people on the same rung, and the gap it reports is to the member one place above — plus the
/// boundary every /api/me read has to hold: another branch's members are not in it.
/// </summary>
public class RankLadderTests : ApplicationTestBase
{
    // Regular opens at 750 and Committed at 2,500, so these three all sit on the Regular rung.
    private const long RegularLow = 900;
    private const long RegularMid = 1_400;
    private const long RegularHigh = 2_100;

    [Fact]
    public async Task The_board_holds_only_the_members_on_the_same_rung()
    {
        var s = await SeedAsync(myXp: RegularMid);
        await AddPeerAsync(s, "Ada", "Lovelace", RegularHigh);
        await AddPeerAsync(s, "Grace", "Hopper", RegularLow);
        // Committed, one rung up. Present at the branch, absent from this board.
        await AddPeerAsync(s, "Alan", "Turing", 3_000);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.OnYourRung.Count.ShouldBe(3);
        ladder.OnYourRung.Select(p => p.DisplayName).ShouldNotContain(n => n.StartsWith("Alan"));
    }

    [Fact]
    public async Task You_are_placed_by_xp_and_marked()
    {
        var s = await SeedAsync(myXp: RegularMid);
        await AddPeerAsync(s, "Ada", "Lovelace", RegularHigh);
        await AddPeerAsync(s, "Grace", "Hopper", RegularLow);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        var you = ladder.OnYourRung.Single(p => p.IsYou);
        you.Position.ShouldBe(2);
        you.Xp.ShouldBe(RegularMid);
        ladder.OnYourRung.Select(p => p.Position).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task The_chase_names_the_member_one_place_above_and_the_real_gap()
    {
        var s = await SeedAsync(myXp: RegularMid);
        await AddPeerAsync(s, "Ada", "Lovelace", RegularHigh);
        await AddPeerAsync(s, "Grace", "Hopper", RegularLow);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.Chasing.ShouldNotBeNull();
        ladder.Chasing.XpAhead.ShouldBe(RegularHigh - RegularMid);
    }

    [Fact]
    public async Task Leading_your_rung_means_there_is_nobody_to_chase()
    {
        // Rather than inventing somebody, or reporting a gap of zero to yourself.
        var s = await SeedAsync(myXp: RegularHigh);
        await AddPeerAsync(s, "Grace", "Hopper", RegularLow);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.OnYourRung.Single(p => p.IsYou).Position.ShouldBe(1);
        ladder.Chasing.ShouldBeNull();
    }

    [Fact]
    public async Task Another_branchs_members_are_not_on_your_board()
    {
        var s = await SeedAsync(myXp: RegularMid);
        await AddPeerAsync(s, "Ada", "Lovelace", RegularHigh, otherBranch: true);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.OnYourRung.ShouldHaveSingleItem().IsYou.ShouldBeTrue();
        ladder.Chasing.ShouldBeNull();
    }

    [Fact]
    public async Task The_ladder_is_served_from_the_policy_with_exactly_one_rung_marked_as_yours()
    {
        var s = await SeedAsync(myXp: RegularMid);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.Rungs.Count.ShouldBe(Enum.GetValues<RankTier>().Length);
        ladder.Rungs.Single(r => r.IsYou).Tier.ShouldBe(nameof(RankTier.Regular));

        // The thresholds are the engine's, not a copy — a client that duplicated them could tell a
        // member they need 6,000 XP for a rung the server opens at 5,000.
        foreach (var rung in ladder.Rungs)
        {
            rung.XpRequired.ShouldBe(RankPolicy.XpRequiredFor(Enum.Parse<RankTier>(rung.Tier)));
        }

        ladder.Rungs.Where(r => r.Reached).Select(r => r.Tier)
            .ShouldBe([nameof(RankTier.Newcomer), nameof(RankTier.Regular)]);
    }

    [Fact]
    public async Task A_member_who_has_earned_nothing_gets_a_pace_of_zero_and_no_estimate()
    {
        var s = await SeedAsync(myXp: RegularMid);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.XpPerWeek.ShouldBe(0);
        ladder.WeeksToNextTier.ShouldBeNull();
    }

    [Fact]
    public async Task The_check_in_tip_reads_attendance_records_not_the_xp_ledger()
    {
        /*
         * The regression this exists for, found on live data rather than in a test.
         *
         * The first version of the tips counted check-ins by dividing GymVisit XP by the award. XP is
         * the ACCOUNTING of an action and the two come apart: seeded history is written straight to
         * the tables without raising the events that award XP, and an award rule can be added long
         * after members have already been doing the thing. The demo member had 26 attendance records
         * in the window and no GymVisit XP at all, so the screen told them "12 of your last 12
         * sessions had no check-in against them" — not merely unhelpful, but plainly false to the
         * person reading it, who had been in the gym twelve times.
         *
         * So: this member checks in every time and earns no XP for it, and must not be nagged.
         */
        var s = await SeedAsync(myXp: RegularMid);
        await LogTrainingAsync(s, days: [1, 3, 5], withCheckIns: true);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.WorkoutsInWindow.ShouldBe(3);
        ladder.Tips.ShouldNotContain(t => t.Code == "check-in");
    }

    [Fact]
    public async Task A_member_who_trains_without_checking_in_is_told_so_with_the_real_count()
    {
        var s = await SeedAsync(myXp: RegularMid);
        await LogTrainingAsync(s, days: [1, 3, 5], withCheckIns: false);

        var ladder = await SendAsync(new GetMyRankLadderQuery());

        ladder.Tips.Single(t => t.Code == "check-in").Detail.ShouldContain("3 of your last 3");
    }

    // ---- harness ----

    private record Seeded(Guid TenantId, Guid BranchId, Guid OtherBranchId, Guid MemberId);

    /// <summary>A session on each of the given days ago, optionally with a check-in beside it.</summary>
    private async Task LogTrainingAsync(Seeded s, int[] days, bool withCheckIns)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        foreach (var ago in days)
        {
            var at = DateTimeProvider.UtcNow.AddDays(-ago);
            db.WorkoutLogs.Add(new WorkoutLog { TenantId = s.TenantId, MemberId = s.MemberId, LoggedAt = at });

            if (withCheckIns)
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    TenantId = s.TenantId,
                    BranchId = s.BranchId,
                    MemberId = s.MemberId,
                    CheckInAt = at,
                    Method = AttendanceMethod.Manual
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<Seeded> SeedAsync(long myXp)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        var other = new Branch { TenantId = tenant.Id, Name = "Other", AddressLine = "2 Main St", City = "City", Country = "US" };
        db.Branches.AddRange(branch, other);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Climbing",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Climbing",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var progression = new MemberProgression { TenantId = tenant.Id, MemberId = member.Id };
        progression.SetTotalXp(myXp);
        db.MemberProgressions.Add(progression);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(tenant.Id, branch.Id, other.Id, member.Id);
    }

    private async Task AddPeerAsync(Seeded s, string first, string last, long xp, bool otherBranch = false)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = new Member
        {
            TenantId = s.TenantId,
            BranchId = otherBranch ? s.OtherBranchId : s.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = first,
            LastName = last,
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var progression = new MemberProgression { TenantId = s.TenantId, MemberId = member.Id };
        progression.SetTotalXp(xp);
        db.MemberProgressions.Add(progression);

        await db.SaveChangesAsync();
    }
}

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
/// The leaderboard is the only surface that shows one member to another, so the things that must
/// hold are: you only ever see your own branch, you never see anyone's full surname, and the score
/// is what the ledgers actually say.
///
/// Clock fixed to Thursday 2026-08-06; that week runs Mon 2026-08-03 .. Sun 2026-08-09.
/// </summary>
public class LeaderboardTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Thursday = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    public LeaderboardTests() => DateTimeProvider.UtcNow = Thursday;

    [Fact]
    public async Task Ranks_my_branch_by_xp_and_marks_my_own_row()
    {
        var ctx = await SeedAsync();
        var rival = await AddMemberAsync(ctx, "Ada", "Lovelace");
        await AwardXpAsync(ctx, ctx.MemberId, 100, Thursday);
        await AwardXpAsync(ctx, rival, 250, Thursday);
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.TotalRanked.ShouldBe(2);
        board.Podium[0].DisplayName.ShouldBe("Ada L.");
        board.Podium[0].Score.ShouldBe(250);
        board.Podium[0].IsYou.ShouldBeFalse();
        board.Podium[1].IsYou.ShouldBeTrue();
        board.You!.Rank.ShouldBe(2);
        board.You.Score.ShouldBe(100);
    }

    [Fact]
    public async Task A_members_board_never_includes_another_branch()
    {
        var ctx = await SeedAsync();
        var sameBranchRival = await AddMemberAsync(ctx, "Ada", "Lovelace");
        var otherBranchId = await AddBranchAsync(ctx);
        var otherBranchMember = await AddMemberAsync(ctx, "Grace", "Hopper", otherBranchId);

        await AwardXpAsync(ctx, ctx.MemberId, 10, Thursday);
        await AwardXpAsync(ctx, sameBranchRival, 20, Thursday);
        await AwardXpAsync(ctx, otherBranchMember, 9999, Thursday);   // would top the board if leaked
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.TotalRanked.ShouldBe(2);
        board.Podium.ShouldNotContain(r => r.DisplayName.StartsWith("Grace"));
        board.Podium.ShouldAllBe(r => r.Score < 9999);
    }

    [Fact]
    public async Task Another_tenant_is_never_visible()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();
        await AwardXpAsync(mine, mine.MemberId, 10, Thursday);
        await AwardXpAsync(theirs, theirs.MemberId, 9999, Thursday);
        AsMember(mine);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.TotalRanked.ShouldBe(1);
        board.You!.Score.ShouldBe(10);
    }

    [Fact]
    public async Task Surnames_are_reduced_to_an_initial()
    {
        var ctx = await SeedAsync();
        await AddMemberAsync(ctx, "Ada", "Lovelace");
        await AwardXpAsync(ctx, ctx.MemberId, 5, Thursday);
        AsMember(ctx);
        var rival = await AddMemberAsync(ctx, "Grace", "Hopper");
        await AwardXpAsync(ctx, rival, 50, Thursday);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.Podium.ShouldNotContain(r => r.DisplayName.Contains("Hopper"));
        board.Podium.ShouldContain(r => r.DisplayName == "Grace H.");
    }

    [Fact]
    public async Task Xp_earned_outside_the_window_does_not_count()
    {
        var ctx = await SeedAsync();
        await AwardXpAsync(ctx, ctx.MemberId, 500, Thursday.AddMonths(-2));   // long before this month
        await AwardXpAsync(ctx, ctx.MemberId, 7, Thursday);
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.You!.Score.ShouldBe(7);
    }

    [Fact]
    public async Task The_weekly_board_is_narrower_than_the_monthly_one()
    {
        var ctx = await SeedAsync();
        // Sat 1 Aug: inside this calendar month, but in the week before the current one.
        await AwardXpAsync(ctx, ctx.MemberId, 60, new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));
        await AwardXpAsync(ctx, ctx.MemberId, 5, Thursday);
        AsMember(ctx);

        var month = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));
        var week = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Week));

        month.You!.Score.ShouldBe(65);
        week.You!.Score.ShouldBe(5);
        week.WindowStart.ShouldBe(new DateOnly(2026, 8, 3));
        week.WindowEnd.ShouldBe(new DateOnly(2026, 8, 9));
    }

    [Fact]
    public async Task Workouts_logged_counts_days_not_entries()
    {
        var ctx = await SeedAsync();
        // Three logs, but only two distinct days.
        await AddWorkoutLogAsync(ctx, ctx.MemberId, Thursday);
        await AddWorkoutLogAsync(ctx, ctx.MemberId, Thursday.AddHours(3));
        await AddWorkoutLogAsync(ctx, ctx.MemberId, Thursday.AddDays(-1));
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.WorkoutsLogged, LeaderboardPeriod.Month));

        board.You!.Score.ShouldBe(2);
    }

    [Fact]
    public async Task Gym_visits_count_days_not_check_ins()
    {
        var ctx = await SeedAsync();
        await AddVisitAsync(ctx, ctx.MemberId, Thursday);
        await AddVisitAsync(ctx, ctx.MemberId, Thursday.AddHours(6));   // same day, second visit
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.GymVisits, LeaderboardPeriod.Month));

        board.You!.Score.ShouldBe(1);
    }

    [Fact]
    public async Task A_member_with_no_activity_is_not_ranked_at_all()
    {
        var ctx = await SeedAsync();
        var rival = await AddMemberAsync(ctx, "Ada", "Lovelace");
        await AwardXpAsync(ctx, rival, 30, Thursday);
        AsMember(ctx);   // I have done nothing

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.You.ShouldBeNull();
        board.YourPercentile.ShouldBeNull();
        board.AroundYou.ShouldBeEmpty();
        board.TotalRanked.ShouldBe(1);
    }

    [Fact]
    public async Task Neighbours_appear_only_when_i_am_off_the_podium()
    {
        var ctx = await SeedAsync();
        // Five rivals all ahead of me, so I land 6th.
        for (var i = 0; i < 5; i++)
        {
            var rival = await AddMemberAsync(ctx, $"Rival{i}", "Test");
            await AwardXpAsync(ctx, rival, 100 + i, Thursday);
        }
        await AwardXpAsync(ctx, ctx.MemberId, 1, Thursday);
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.You!.Rank.ShouldBe(6);
        board.Podium.Count.ShouldBe(3);
        board.AroundYou.ShouldNotBeEmpty();
        board.AroundYou.ShouldContain(r => r.IsYou);
        board.YourPercentile.ShouldBe(17);   // 6th of 6
    }

    [Fact]
    public async Task Topping_the_board_needs_no_neighbour_strip()
    {
        var ctx = await SeedAsync();
        await AwardXpAsync(ctx, ctx.MemberId, 100, Thursday);
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.XpEarned, LeaderboardPeriod.Month));

        board.You!.Rank.ShouldBe(1);
        board.AroundYou.ShouldBeEmpty();
        board.YourPercentile.ShouldBe(100);
    }

    [Fact]
    public async Task Streak_board_reports_the_current_run()
    {
        var ctx = await SeedAsync();
        await AddWorkoutLogAsync(ctx, ctx.MemberId, Thursday);
        await AddWorkoutLogAsync(ctx, ctx.MemberId, Thursday.AddDays(-7));
        AsMember(ctx);

        var board = await SendAsync(new GetMyLeaderboardQuery(LeaderboardCategory.WeeklyStreak, LeaderboardPeriod.Month));

        board.You!.Score.ShouldBe(2);
    }

    private void AsMember(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> AddBranchAsync(SeedContext ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var branch = new Branch { TenantId = ctx.TenantId, Name = "Second", AddressLine = "2 Side St", City = "City", Country = "US" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    private async Task<Guid> AddMemberAsync(SeedContext ctx, string first, string last, Guid? branchId = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = new Member
        {
            TenantId = ctx.TenantId, BranchId = branchId ?? ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = first, LastName = last,
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task AwardXpAsync(SeedContext ctx, Guid memberId, int amount, DateTimeOffset at)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.XpTransactions.Add(new XpTransaction
        {
            TenantId = ctx.TenantId, MemberId = memberId, Amount = amount,
            Reason = XpReason.WorkoutCompleted, SourceType = XpSourceType.WorkoutLog, OccurredAt = at
        });
        await db.SaveChangesAsync();
    }

    private async Task AddWorkoutLogAsync(SeedContext ctx, Guid memberId, DateTimeOffset at)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.WorkoutLogs.Add(new WorkoutLog { MemberId = memberId, LoggedAt = at });
        await db.SaveChangesAsync();
    }

    private async Task AddVisitAsync(SeedContext ctx, Guid memberId, DateTimeOffset at)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, MemberId = memberId,
            CheckInAt = at, Method = AttendanceMethod.Manual
        });
        await db.SaveChangesAsync();
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId, Guid MemberUserId);

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

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, member.Id, user.Id);
    }
}

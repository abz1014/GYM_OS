using FluentValidation;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// The two freeze defects, driven end to end through the real MediatR pipeline rather than asserted
/// against the policy in isolation. Both were found by exercising a live database, and both are
/// reproduced here first so the tests fail against the old code for the reason they claim to.
///
/// ONE: freezing minted membership time. The plan's MaxFreezeDays was checked against a single
/// request with no memory of what had been spent, and resuming credited the whole requested window
/// onto EndDate whether or not any of it had elapsed. Freezing a window that had not started yet and
/// immediately resuming therefore paid out its full length, repeatably — three cycles of one 30-day
/// window moved a real paid-up-to date from 2027-07-21 to 2027-10-19.
///
/// TWO: freezing resurrected dead memberships. Cancel refuses Expired/Cancelled/Transferred, Resume
/// requires Frozen, Reactivate requires Cancelled — and Freeze accepted any status at all. So
/// cancel -> freeze -> resume returned a membership to Active carrying the CancellationReason that
/// killed it, without ever passing through Reactivate.
/// </summary>
public class MembershipFreezeTests : ApplicationTestBase
{
    // The clock the pipeline sees. Freeze windows below are placed relative to this on purpose:
    // "in the future" and "already running" are different situations and the bug lived in the gap.
    private static readonly DateOnly Today = new(2026, 1, 15);

    // ---- one: a freeze cannot mint membership time ----

    [Fact]
    public async Task Freezing_and_resuming_an_untouched_future_window_gives_back_nothing()
    {
        /*
         * The exploit, minimised. The window starts a fortnight out; the resume happens immediately.
         * Nothing was paused, so nothing may be credited. This previously added the full 10 days.
         */
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);
        var endDateBefore = await EndDateAsync(w.MembershipId);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(14), Today.AddDays(24)));
        await SendAsync(new ResumeMembershipCommand(w.MembershipId));

        var membership = await LoadAsync(w.MembershipId);
        membership.EndDate.ShouldBe(endDateBefore);
        membership.FreezeDaysUsed.ShouldBe(0);
        membership.Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task Repeating_the_cycle_cannot_move_the_paid_up_date_at_all()
    {
        // The live repro, verbatim in shape: same window, three times over. It used to march EndDate
        // forward by its full length on every pass, with nothing bounding a fourth.
        var w = await SeedAsync(maxFreezeDays: 30);
        SignIn(w);
        var endDateBefore = await EndDateAsync(w.MembershipId);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(17), Today.AddDays(47)));
            await SendAsync(new ResumeMembershipCommand(w.MembershipId));
        }

        (await LoadAsync(w.MembershipId)).EndDate.ShouldBe(endDateBefore);
    }

    [Fact]
    public async Task A_real_freeze_still_pauses_the_clock_for_the_days_it_actually_ran()
    {
        /*
         * The negative control, and the point of the whole feature: a member who genuinely stops
         * training must not lose the time they paid for. Frozen five days ago, resumed today, so
         * five days go back on the end date — not the fourteen that were asked for.
         */
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);
        var endDateBefore = await EndDateAsync(w.MembershipId);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-5), Today.AddDays(9)));
        await SendAsync(new ResumeMembershipCommand(w.MembershipId));

        var membership = await LoadAsync(w.MembershipId);
        membership.EndDate.ShouldBe(endDateBefore.AddDays(5));
        membership.FreezeDaysUsed.ShouldBe(5);
        membership.Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task Resuming_clears_the_window_so_it_cannot_be_paid_for_twice()
    {
        // Half the fix. Even with the credit corrected, leaving the dates on the row meant the next
        // resume re-read the same window and paid it again.
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-5), Today.AddDays(9)));
        await SendAsync(new ResumeMembershipCommand(w.MembershipId));

        var membership = await LoadAsync(w.MembershipId);
        membership.FreezeStartDate.ShouldBeNull();
        membership.FreezeEndDate.ShouldBeNull();
    }

    [Fact]
    public async Task The_plan_allowance_is_spent_across_freezes_not_reset_by_each_one()
    {
        /*
         * The other half. A 14-day plan, 5 days already used, then a 10-day request: under the old
         * rule that passed, because 10 <= 14 and nothing counted. It must now be refused, and the
         * refusal must say what is left rather than repeating the plan's headline number.
         */
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-5), Today.AddDays(9)));
        await SendAsync(new ResumeMembershipCommand(w.MembershipId));

        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(10))));
        refused.Message.ShouldContain("9 of its 14 freeze day(s) left");

        // And the remainder is genuinely still usable — the guard limits, it does not lock out.
        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(9)));
        (await LoadAsync(w.MembershipId)).Status.ShouldBe(MemberMembershipStatus.Frozen);
    }

    [Fact]
    public async Task A_window_already_entirely_in_the_past_is_refused_end_to_end()
    {
        /*
         * Live exploit against the first version of this fix: freeze 2020-01-01..31 on a membership
         * created in 2026, resume seconds later, and 30 days were credited for time the member spent
         * training. Bounded by the allowance, but still minted. A window that is already over pauses
         * nothing — it is a retroactive credit wearing a freeze's clothes.
         */
        var w = await SeedAsync(maxFreezeDays: 30);
        SignIn(w);

        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-20), Today.AddDays(-1))));
        refused.Message.ShouldContain("already over");

        (await LoadAsync(w.MembershipId)).Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task Cancelling_mid_freeze_settles_the_ledger_and_reactivation_returns_no_window()
    {
        /*
         * The side door: Freeze -> Cancel -> Reactivate instead of Resume used to skip the ledger
         * entirely — the days spent frozen were never charged to FreezeDaysUsed, and the window rode
         * through Cancelled back onto an ACTIVE row, waiting to be converted into free time by the
         * next thing that flipped the row through Frozen. Cancel now settles the freeze exactly as a
         * resume would, and Reactivate defensively clears any window that predates that rule.
         */
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);
        var endDateBefore = await EndDateAsync(w.MembershipId);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-5), Today.AddDays(9)));
        await SendAsync(new CancelMembershipCommand(w.MembershipId, "moving away"));

        var cancelled = await LoadAsync(w.MembershipId);
        cancelled.Status.ShouldBe(MemberMembershipStatus.Cancelled);
        cancelled.FreezeDaysUsed.ShouldBe(5);              // the five elapsed days, charged
        cancelled.EndDate.ShouldBe(endDateBefore.AddDays(5)); // and credited, exactly like a resume
        cancelled.FreezeStartDate.ShouldBeNull();
        cancelled.FreezeEndDate.ShouldBeNull();

        await SendAsync(new ReactivateMembershipCommand(w.MembershipId));

        var reactivated = await LoadAsync(w.MembershipId);
        reactivated.Status.ShouldBe(MemberMembershipStatus.Active);
        reactivated.FreezeStartDate.ShouldBeNull();
        reactivated.FreezeEndDate.ShouldBeNull();

        // And the ledger carried through: 9 of 14 remain, so 10 is refused and 9 is not.
        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(10))));
        refused.Message.ShouldContain("9 of its 14 freeze day(s) left");
    }

    // ---- two: a freeze cannot resurrect a dead membership ----

    [Fact]
    public async Task A_cancelled_membership_cannot_be_frozen()
    {
        // Live repro: cancel with a reason, then freeze inside the plan's allowance, and the row came
        // back as "Frozen | member quit" — a paying status re-entered through the one command that
        // never checked where it was starting from.
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);

        await SendAsync(new CancelMembershipCommand(w.MembershipId, "member quit"));

        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(5))));
        refused.Message.ShouldContain("Only an active membership can be frozen");

        var membership = await LoadAsync(w.MembershipId);
        membership.Status.ShouldBe(MemberMembershipStatus.Cancelled);
        membership.CancellationReason.ShouldBe("member quit");
    }

    [Fact]
    public async Task An_expired_membership_cannot_be_frozen()
    {
        // Same hole, reached without touching Cancel: an expired row is equally not a paying one.
        var w = await SeedAsync(maxFreezeDays: 14, status: MemberMembershipStatus.Expired);
        SignIn(w);

        await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(5))));

        (await LoadAsync(w.MembershipId)).Status.ShouldBe(MemberMembershipStatus.Expired);
    }

    [Fact]
    public async Task An_already_frozen_membership_is_refused_in_its_own_words()
    {
        /*
         * Refused separately from the dead statuses because it is a different request wearing the
         * same name: extending a freeze would have to decide what happens to the untaken remainder
         * of the first one, and silently overwriting the window was how days went missing.
         */
        var w = await SeedAsync(maxFreezeDays: 30);
        SignIn(w);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(5)));

        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today, Today.AddDays(6))));
        refused.Message.ShouldContain("already frozen");

        // The first window survives the rejected second request untouched.
        var membership = await LoadAsync(w.MembershipId);
        membership.FreezeEndDate.ShouldBe(Today.AddDays(5));
    }

    [Fact]
    public async Task The_batch_freeze_spends_the_same_allowance_as_the_single_one()
    {
        /*
         * Two doors into one rule. The members-list batch action calls the same policy, and it has to
         * be handed the same FreezeDaysUsed — otherwise selecting a member whose allowance is spent
         * hands it straight back to them.
         */
        var w = await SeedAsync(maxFreezeDays: 14);
        SignIn(w);

        await SendAsync(new FreezeMembershipCommand(w.MembershipId, Today.AddDays(-14), Today));
        await SendAsync(new ResumeMembershipCommand(w.MembershipId));
        (await LoadAsync(w.MembershipId)).FreezeDaysUsed.ShouldBe(14);

        var result = await SendAsync(new BatchFreezeMembershipsCommand(
            [w.MemberId], Today, Today.AddDays(3)));

        result.Succeeded.ShouldBe(0);
        result.Failed.ShouldBe(1);
        result.Outcomes.ShouldHaveSingleItem();
        result.Outcomes[0].Reason.ShouldNotBeNull().ShouldContain("0 of its 14 freeze day(s) left");
        (await LoadAsync(w.MembershipId)).Status.ShouldBe(MemberMembershipStatus.Active);
    }

    // ---- harness ----

    private void SignIn(World w)
    {
        CurrentUser.TenantId = w.TenantId;
        CurrentUser.UserId = w.StaffUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<MemberMembership> LoadAsync(Guid membershipId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.MemberMemberships.AsNoTracking().SingleAsync(m => m.Id == membershipId);
    }

    private async Task<DateOnly> EndDateAsync(Guid membershipId) => (await LoadAsync(membershipId)).EndDate;

    private record World(Guid TenantId, Guid StaffUserId, Guid MemberId, Guid MembershipId);

    private async Task<World> SeedAsync(int maxFreezeDays, MemberMembershipStatus status = MemberMembershipStatus.Active)
    {
        DateTimeProvider.UtcNow = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US"
        };
        db.Branches.Add(branch);

        // AuditLog.UserId is a real FK — the pipeline's audit write needs a User row behind the id.
        var staff = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test", FirstName = "Staff", LastName = "User"
        };
        db.Users.Add(staff);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staff.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Freeze", LastName = "Subject",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var plan = new MembershipPlan
        {
            TenantId = tenant.Id, Name = "Annual", Type = MembershipPlanType.Annual,
            DurationDays = 365, Price = 449.99m, Currency = "USD", MaxFreezeDays = maxFreezeDays
        };
        db.MembershipPlans.Add(plan);

        var membership = new MemberMembership
        {
            MemberId = member.Id, MembershipPlanId = plan.Id,
            StartDate = Today.AddDays(-30),
            // Well clear of today so a resume never lands on the Expired branch by accident.
            EndDate = Today.AddDays(335),
            Status = status, PricePaid = 449.99m, Currency = "USD"
        };
        db.MemberMemberships.Add(membership);

        await db.SaveChangesAsync();
        return new World(tenant.Id, staff.Id, member.Id, membership.Id);
    }
}

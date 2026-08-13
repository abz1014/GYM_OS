using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Classes.Commands;
using GymOS.Application.Modules.Classes.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Classes;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Classes;

/// <summary>
/// The booking state machine end-to-end through the real handlers + DB: a full session waitlists
/// the next member, and cancelling a confirmed spot promotes the longest-waiting one. This is the
/// behaviour that makes class booking actually usable, so it's proven against the database rather
/// than only in the pure ClassBookingPolicy unit tests.
/// </summary>
public class ClassBookingCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Booking_past_capacity_waitlists_and_cancelling_a_spot_promotes_the_next_in_line()
    {
        var ctx = await SeedAsync(capacity: 1);
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;

        // First member takes the single spot; second and third are waitlisted in booking order.
        (await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberA))).ShouldBe(ClassBookingStatus.Booked);
        (await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberB))).ShouldBe(ClassBookingStatus.Waitlisted);
        (await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberC))).ShouldBe(ClassBookingStatus.Waitlisted);

        var rosterBefore = await SendAsync(new GetClassSessionRosterQuery(ctx.SessionId));
        rosterBefore.BookedCount.ShouldBe(1);
        rosterBefore.WaitlistCount.ShouldBe(2);

        // Cancel the confirmed member — the earliest waitlisted (MemberB) should be promoted.
        var memberABooking = await BookingIdFor(ctx.SessionId, ctx.MemberA);
        await SendAsync(new CancelClassBookingCommand(memberABooking));

        var rosterAfter = await SendAsync(new GetClassSessionRosterQuery(ctx.SessionId));
        rosterAfter.BookedCount.ShouldBe(1);
        rosterAfter.WaitlistCount.ShouldBe(1);
        rosterAfter.Bookings.Single(b => b.Status == ClassBookingStatus.Booked).MemberId.ShouldBe(ctx.MemberB);
        rosterAfter.Bookings.Single(b => b.Status == ClassBookingStatus.Waitlisted).MemberId.ShouldBe(ctx.MemberC);
    }

    [Fact]
    public async Task A_member_cannot_hold_two_active_bookings_for_the_same_session()
    {
        var ctx = await SeedAsync(capacity: 10);
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;

        await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberA));

        var act = () => SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberA));

        await Should.ThrowAsync<FluentValidation.ValidationException>(act);
    }

    [Fact]
    public async Task Cancelling_the_session_releases_every_active_booking()
    {
        var ctx = await SeedAsync(capacity: 1);
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;

        await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberA)); // Booked
        await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberB)); // Waitlisted

        await SendAsync(new CancelClassSessionCommand(ctx.SessionId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var statuses = await db.ClassBookings.Where(b => b.ClassSessionId == ctx.SessionId).Select(b => b.Status).ToListAsync();

        statuses.ShouldAllBe(s => s == ClassBookingStatus.Cancelled);
    }

    [Fact]
    public async Task Deactivating_a_schedule_releases_the_bookings_on_its_cancelled_sessions()
    {
        /*
         * Found on a member's own Today page: "we stopped running Monday Spin" cancelled the future
         * sessions but left their bookings Booked, so members were still told to turn up. The slot's
         * discontinuation must release its people exactly as cancelling one session does.
         */
        var ctx = await SeedAsync(capacity: 5);
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;

        Guid scheduleId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var session = await db.ClassSessions.SingleAsync(s => s.Id == ctx.SessionId);
            var schedule = new ClassSchedule
            {
                TenantId = ctx.TenantId, BranchId = session.BranchId, ClassTypeId = session.ClassTypeId,
                DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(18, 0),
                DurationMinutes = 45, Capacity = 5, IsActive = true
            };
            db.ClassSchedules.Add(schedule);
            session.ClassScheduleId = schedule.Id;
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberA));
        await SendAsync(new BookClassSessionCommand(ctx.SessionId, ctx.MemberB));

        await SendAsync(new SetClassScheduleActiveCommand(scheduleId, IsActive: false));

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await verifyDb.ClassSessions.SingleAsync(s => s.Id == ctx.SessionId))
            .Status.ShouldBe(ClassSessionStatus.Cancelled);
        (await verifyDb.ClassBookings.Where(b => b.ClassSessionId == ctx.SessionId).Select(b => b.Status).ToListAsync())
            .ShouldAllBe(s => s == ClassBookingStatus.Cancelled);
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_active_booking_when_application_checks_are_bypassed()
    {
        /*
         * The handler's "already booked" guard is a read-then-write, and under concurrency every
         * racer passes the read — one live member ended up with three simultaneous Booked rows on
         * one session. The partial unique index is the version of the rule races cannot beat; this
         * test writes around the handler on purpose to prove the DATABASE holds the line.
         */
        var ctx = await SeedAsync(capacity: 10);
        // Reads below run through the fail-closed tenant filter, handler or no handler.
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var session = await db.ClassSessions.SingleAsync(s => s.Id == ctx.SessionId);

        ClassBooking Raw(ClassBookingStatus status) => new()
        {
            TenantId = ctx.TenantId, BranchId = session.BranchId,
            ClassSessionId = ctx.SessionId, MemberId = ctx.MemberA,
            Status = status, BookedAt = DateTimeOffset.UtcNow
        };

        db.ClassBookings.Add(Raw(ClassBookingStatus.Booked));
        await db.SaveChangesAsync();

        db.ClassBookings.Add(Raw(ClassBookingStatus.Waitlisted));
        await Should.ThrowAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // A cancelled row does not block a fresh booking — rebooking after cancelling is normal.
        var active = await db.ClassBookings.SingleAsync(b => b.ClassSessionId == ctx.SessionId && b.MemberId == ctx.MemberA);
        active.Status = ClassBookingStatus.Cancelled;
        await db.SaveChangesAsync();

        db.ClassBookings.Add(Raw(ClassBookingStatus.Booked));
        await db.SaveChangesAsync();
    }

    private async Task<Guid> BookingIdFor(Guid sessionId, Guid memberId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.ClassBookings
            .Where(b => b.ClassSessionId == sessionId && b.MemberId == memberId && b.Status != ClassBookingStatus.Cancelled)
            .Select(b => b.Id)
            .FirstAsync();
    }

    private async Task<(Guid TenantId, Guid StaffUserId, Guid SessionId, Guid MemberA, Guid MemberB, Guid MemberC)> SeedAsync(int capacity)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var staff = new GymOS.Domain.Identity.User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused",
            FirstName = "Front",
            LastName = "Desk"
        };
        db.Users.Add(staff);

        var members = Enumerable.Range(0, 3).Select(i => new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = $"Member{i}",
            LastName = "Test",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        }).ToList();
        db.Members.AddRange(members);

        var classType = new ClassType { TenantId = tenant.Id, Name = "Spin", DefaultDurationMinutes = 45, DefaultCapacity = capacity };
        db.ClassTypes.Add(classType);

        var session = new ClassSession
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClassTypeId = classType.Id,
            StartsAt = new DateTimeOffset(new DateTime(2026, 8, 10, 18, 0, 0), TimeSpan.Zero),
            DurationMinutes = 45,
            Capacity = capacity,
            Status = ClassSessionStatus.Scheduled
        };
        db.ClassSessions.Add(session);

        // Staff needs branch access for the roster query's branch-scoping.
        db.UserBranchAccesses.Add(new GymOS.Domain.Identity.UserBranchAccess { UserId = staff.Id, BranchId = branch.Id });

        await db.SaveChangesAsync();
        return (tenant.Id, staff.Id, session.Id, members[0].Id, members[1].Id, members[2].Id);
    }
}

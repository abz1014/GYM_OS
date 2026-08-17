using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Classes;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Notifications;
using GymOS.Domain.Settings;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The member's ACCOUNT — their money, their contract, their contact details — as opposed to their
/// training, which the rest of the portal already covered.
///
/// Two defects run through everything here and both are pinned below.
///
/// ONE: whose account. Every staff-facing equivalent of these reads and writes takes an id — a
/// member id on the invoice list, a membership id on freeze/resume, a contact id on an emergency
/// contact. Those are correct for staff, who are gated by a permission that grants the whole branch.
/// Reachable by the Member role they are a directory: one member reads another member's invoices, or
/// edits the number that gets called when somebody else collapses. So every case here seeds TWO
/// members in the SAME tenant and branch — tenant isolation must not be what makes these pass — and
/// asserts the second one's records are invisible or not-found, never forbidden.
///
/// TWO: the member portal must not become a second, laxer door into rules that already exist.
/// Freezing in particular is governed by an allowance that took a live-data incident to get right
/// (see MembershipFreezePolicy and MembershipFreezeTests). The member-facing freeze delegates into
/// that exact command rather than reimplementing it, and the test below proves it by demanding the
/// policy's own sentence back.
/// </summary>
public class MyAccountTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Wednesday = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 8, 5);

    public MyAccountTests() => DateTimeProvider.UtcNow = Wednesday;

    // ---- invoices: my money, and only mine ----

    [Fact]
    public async Task My_invoice_list_contains_my_invoices_and_nobody_elses()
    {
        var w = await SeedAsync();
        var mineOlder = await AddInvoiceAsync(w, w.Mine.MemberId, "INV-1001", Today.AddDays(-30), 100m);
        var mineNewer = await AddInvoiceAsync(w, w.Mine.MemberId, "INV-1002", Today.AddDays(-2), 60m);
        await AddInvoiceAsync(w, w.Theirs.MemberId, "INV-1003", Today.AddDays(-1), 80m);

        SignIn(w.Mine);
        var invoices = await SendAsync(new GetMyInvoicesQuery());

        // Newest first, and the third invoice — issued most recently of all — belongs to the other
        // member sharing this branch, so it must not be at the top of this list or in it at all.
        invoices.Select(i => i.Id).ShouldBe([mineNewer, mineOlder]);
        invoices.ShouldAllBe(i => i.InvoiceNumber != "INV-1003");
    }

    [Fact]
    public async Task Only_completed_payments_count_towards_what_the_member_has_paid()
    {
        // A pending card attempt is not money the gym has. Counting it would tell a member their
        // bill was settled while the retry job was still chasing it.
        var w = await SeedAsync();
        var invoiceId = await AddInvoiceAsync(w, w.Mine.MemberId, "INV-2001", Today.AddDays(-3), 100m);
        await AddPaymentAsync(invoiceId, 40m, PaymentStatus.Completed);
        await AddPaymentAsync(invoiceId, 25m, PaymentStatus.Pending);
        await AddPaymentAsync(invoiceId, 15m, PaymentStatus.Failed);

        SignIn(w.Mine);
        var invoice = (await SendAsync(new GetMyInvoicesQuery())).Single();

        invoice.TotalAmount.ShouldBe(100m);
        invoice.PaidAmount.ShouldBe(40m);
    }

    // ---- membership: the member's own contract, through the staff rules ----

    [Fact]
    public async Task A_member_freeze_beyond_the_plan_allowance_is_refused_in_the_policys_own_words()
    {
        /*
         * The reason this endpoint delegates rather than implements. The allowance is a property of
         * the PLAN and is cumulative across every freeze a membership has ever had — the rule that a
         * live database was caught minting membership time without. A member-facing freeze that
         * skipped it would hand the whole allowance back to the one person it exists to bound, and
         * would do it from a phone.
         *
         * The assertion is on the sentence, not just the refusal: getting a ValidationException from
         * some validator of the portal's own would pass a weaker test while proving nothing about
         * which code decided.
         */
        var w = await SeedAsync(maxFreezeDays: 7);
        SignIn(w.Mine);

        var refused = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new FreezeMyMembershipCommand(Today, Today.AddDays(30))));

        refused.Message.ShouldContain("This plan allows at most 7 freeze day(s); 30 requested.");

        // And nothing moved: a refused freeze must leave the membership exactly as it was.
        var membership = await LoadMembershipAsync(w.MyMembershipId);
        membership.Status.ShouldBe(MemberMembershipStatus.Active);
        membership.FreezeStartDate.ShouldBeNull();
    }

    [Fact]
    public async Task A_freeze_inside_the_allowance_pauses_the_membership_and_resume_restarts_it()
    {
        var w = await SeedAsync(maxFreezeDays: 30);
        SignIn(w.Mine);

        await SendAsync(new FreezeMyMembershipCommand(Today, Today.AddDays(10)));

        var frozen = await LoadMembershipAsync(w.MyMembershipId);
        frozen.Status.ShouldBe(MemberMembershipStatus.Frozen);
        frozen.FreezeEndDate.ShouldBe(Today.AddDays(10));

        await SendAsync(new ResumeMyMembershipCommand());

        var resumed = await LoadMembershipAsync(w.MyMembershipId);
        resumed.Status.ShouldBe(MemberMembershipStatus.Active);
        // Resumed on day zero of the window, so nothing was actually paused and nothing is credited —
        // the anti-minting rule, reached through the member's own door.
        resumed.FreezeDaysUsed.ShouldBe(0);
    }

    [Fact]
    public async Task One_members_freeze_never_touches_another_members_membership()
    {
        // No membership id crosses the wire, so there is nothing to tamper with — but the resolver
        // could still pick the wrong row. It must pick from the caller's own memberships only.
        var w = await SeedAsync(maxFreezeDays: 30);
        SignIn(w.Mine);

        await SendAsync(new FreezeMyMembershipCommand(Today, Today.AddDays(5)));

        (await LoadMembershipAsync(w.TheirMembershipId)).Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task A_cancellation_request_is_a_note_signed_by_the_member_not_a_status_change()
    {
        /*
         * The request has to reach staff where they already look, and it must not execute itself.
         * Cancellation carries notice periods, refunds and a retention conversation; a self-service
         * button that performed it would be the one irreversible action in the portal, one mis-tap
         * from a membership nobody meant to end.
         */
        var w = await SeedAsync();
        SignIn(w.Mine);

        await SendAsync(new RequestMyCancellationCommand("Moving abroad in September"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var note = await db.MemberNotes.AsNoTracking().SingleAsync(n => n.MemberId == w.Mine.MemberId);
        note.Note.ShouldBe("Requested cancellation from the member portal: Moving abroad in September");

        // Attributed to the member's own user. A note in a shared inbox whose author is a staff id
        // reads as "the desk decided this", which is the opposite of what happened.
        note.AuthorUserId.ShouldBe(w.Mine.UserId);

        // The contract itself is untouched — this is a request, not a cancellation.
        (await LoadMembershipAsync(w.MyMembershipId)).Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task Turning_auto_renew_off_tells_staff_and_turning_it_on_does_not_nag_them()
    {
        // A silent flag flip is invisible to the people whose job it is to keep the member: by the
        // time the renewal simply fails to happen, the conversation that might have saved it is a
        // month late.
        var w = await SeedAsync(autoRenew: true);
        SignIn(w.Mine);

        await SendAsync(new SetMyAutoRenewCommand(false));

        (await LoadMembershipAsync(w.MyMembershipId)).AutoRenew.ShouldBeFalse();

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var note = await db.MemberNotes.AsNoTracking().SingleAsync(n => n.MemberId == w.Mine.MemberId);
            note.Note.ShouldBe("Turned off auto-renew from the member portal.");
            note.AuthorUserId.ShouldBe(w.Mine.UserId);
        }

        // Turning it back on is good news and needs no note; and re-sending "off" when it is already
        // off must not stack a second identical note on top of the member's real history.
        await SendAsync(new SetMyAutoRenewCommand(true));
        await SendAsync(new SetMyAutoRenewCommand(false));
        await SendAsync(new SetMyAutoRenewCommand(false));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            (await db.MemberNotes.AsNoTracking().CountAsync(n => n.MemberId == w.Mine.MemberId)).ShouldBe(2);
        }
    }

    // ---- waitlist position ----

    [Fact]
    public async Task My_waitlist_position_is_one_based_and_ordered_by_when_people_joined_the_queue()
    {
        /*
         * "Waitlisted" on its own answers the wrong question. First in line should keep the evening
         * free; eleventh should make other plans, and the app was telling both of them the same
         * thing. The number reported is the promotion queue's own order — earliest BookedAt is
         * promoted first — so it must follow BookedAt and not the order rows happen to be inserted,
         * which is why the three bookings below are written out of order on purpose.
         */
        var w = await SeedAsync();
        var sessionId = await AddSessionAsync(w, Wednesday.AddHours(3));

        await AddBookingAsync(w, sessionId, w.Mine.MemberId, ClassBookingStatus.Waitlisted, Wednesday.AddHours(-2));
        await AddBookingAsync(w, sessionId, w.Bystander, ClassBookingStatus.Waitlisted, Wednesday.AddHours(-1));
        await AddBookingAsync(w, sessionId, w.Theirs.MemberId, ClassBookingStatus.Waitlisted, Wednesday.AddHours(-3));

        SignIn(w.Mine);
        (await SendAsync(new GetMyClassBookingsQuery())).Single().WaitlistPosition.ShouldBe(2);

        // The member who joined first is told 1, not 0 — nobody says "you are 0th in the queue".
        SignIn(w.Theirs);
        (await SendAsync(new GetMyClassBookingsQuery())).Single().WaitlistPosition.ShouldBe(1);
    }

    [Fact]
    public async Task A_confirmed_booking_has_no_waitlist_position_at_all()
    {
        // Null rather than 0: a member holding a spot is not at the front of a queue, they are not
        // in one, and a 0 would render as a position the first time a UI checked for a number.
        var w = await SeedAsync();
        var sessionId = await AddSessionAsync(w, Wednesday.AddHours(3));
        await AddBookingAsync(w, sessionId, w.Mine.MemberId, ClassBookingStatus.Booked, Wednesday.AddHours(-2));

        SignIn(w.Mine);

        (await SendAsync(new GetMyClassBookingsQuery())).Single().WaitlistPosition.ShouldBeNull();
    }

    // ---- class history ----

    [Fact]
    public async Task Class_history_reports_what_already_happened_including_the_ones_i_missed()
    {
        var w = await SeedAsync();
        var attended = await AddSessionAsync(w, Wednesday.AddDays(-2));
        var missed = await AddSessionAsync(w, Wednesday.AddDays(-1));
        var released = await AddSessionAsync(w, Wednesday.AddDays(-3));
        var upcoming = await AddSessionAsync(w, Wednesday.AddDays(1));

        await AddBookingAsync(w, attended, w.Mine.MemberId, ClassBookingStatus.CheckedIn, Wednesday.AddDays(-3));
        await AddBookingAsync(w, missed, w.Mine.MemberId, ClassBookingStatus.NoShow, Wednesday.AddDays(-3));
        await AddBookingAsync(w, released, w.Mine.MemberId, ClassBookingStatus.Cancelled, Wednesday.AddDays(-4));
        await AddBookingAsync(w, upcoming, w.Mine.MemberId, ClassBookingStatus.Booked, Wednesday.AddDays(-1));

        SignIn(w.Mine);
        var history = await SendAsync(new GetMyClassHistoryQuery());

        // Newest first; the missed class is shown as missed rather than quietly dropped, the
        // released booking is not a class the member went to, and a class still to come is not
        // history at all.
        history.Select(h => h.Status).ShouldBe([ClassBookingStatus.NoShow, ClassBookingStatus.CheckedIn]);
        history.First().StartsAt.ShouldBe(Wednesday.AddDays(-1));
    }

    // ---- the gym itself ----

    [Fact]
    public async Task The_gym_card_names_the_members_own_branch_and_the_gyms_support_contacts()
    {
        // Six member-facing strings said "ask the front desk" while the app refused to say where the
        // desk was or what its number was.
        var w = await SeedAsync();
        SignIn(w.Mine);

        var gym = await SendAsync(new GetMyGymQuery());

        gym.BranchName.ShouldBe("Riverside");
        gym.AddressLine.ShouldBe("1 Main St");
        gym.SupportEmail.ShouldBe("help@titan.example");
        gym.SupportPhone.ShouldBe("+1-555-0100");
    }

    // ---- emergency contacts ----

    [Fact]
    public async Task I_can_keep_my_own_emergency_contact_current()
    {
        // The one record whose whole purpose is to be right on the worst day, and until now only
        // somebody else could correct it — in person, during opening hours.
        var w = await SeedAsync();
        SignIn(w.Mine);

        var contactId = await SendAsync(new AddMyEmergencyContactCommand("Dana Okafor", "+1-555-0111", "Partner"));
        await SendAsync(new UpdateMyEmergencyContactCommand(contactId, "Dana Okafor", "+1-555-0999", "Partner"));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var contact = await db.EmergencyContacts.AsNoTracking().SingleAsync(c => c.Id == contactId);
            contact.MemberId.ShouldBe(w.Mine.MemberId);
            contact.Phone.ShouldBe("+1-555-0999");
        }

        await SendAsync(new DeleteMyEmergencyContactCommand(contactId));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            (await db.EmergencyContacts.AsNoTracking().AnyAsync(c => c.Id == contactId)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Another_members_emergency_contact_is_not_found_rather_than_forbidden()
    {
        // 404, never 403. A forbidden answer confirms the id exists, which turns these endpoints
        // into an oracle for probing other members' records one guess at a time.
        var w = await SeedAsync();

        SignIn(w.Theirs);
        var theirContactId = await SendAsync(new AddMyEmergencyContactCommand("Someone Else", "+1-555-0222", "Sibling"));

        SignIn(w.Mine);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new UpdateMyEmergencyContactCommand(theirContactId, "Hijacked", "+1-555-0000", "Partner")));
        await Should.ThrowAsync<NotFoundException>(async () =>
            await SendAsync(new DeleteMyEmergencyContactCommand(theirContactId)));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var contact = await db.EmergencyContacts.AsNoTracking().SingleAsync(c => c.Id == theirContactId);
        contact.Name.ShouldBe("Someone Else");
    }

    // ---- profile ----

    [Fact]
    public async Task Correcting_my_phone_number_changes_my_row_and_only_my_row()
    {
        var w = await SeedAsync();
        SignIn(w.Mine);

        await SendAsync(new UpdateMyProfileCommand("+1-555-0777"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Members.AsNoTracking().SingleAsync(m => m.Id == w.Mine.MemberId)).Phone.ShouldBe("+1-555-0777");
        (await db.Members.AsNoTracking().SingleAsync(m => m.Id == w.Theirs.MemberId)).Phone.ShouldBeNull();
    }

    // ---- notifications ----

    [Fact]
    public async Task My_notifications_are_the_ones_addressed_to_me_and_already_due()
    {
        var w = await SeedAsync();
        await AddNotificationAsync(w, w.Mine.MemberId, Wednesday.AddDays(-1));
        await AddNotificationAsync(w, w.Mine.MemberId, Wednesday.AddDays(3));       // not yet sent
        await AddNotificationAsync(w, w.Theirs.MemberId, Wednesday.AddHours(-1));   // not mine

        SignIn(w.Mine);
        var notifications = await SendAsync(new GetMyNotificationsQuery());

        var only = notifications.ShouldHaveSingleItem();
        only.OccurredAt.ShouldBe(Wednesday.AddDays(-1));
        only.Channel.ShouldBe(NotificationChannel.InApp);

        // The template's placeholders are substituted for the member's own name, because the
        // rendered copy the dispatch job produces is written to a log with no member link on it —
        // without this the feed opens every message with a literal "Hi {{FirstName}}".
        only.Title.ShouldBe("Your membership is expiring");
        only.Body.ShouldBe("Hi Mine, renew to keep your access.");
    }

    // ---- harness ----

    private record Person(Guid MemberId, Guid UserId);

    private record World(
        Guid TenantId, Guid BranchId, Guid ClassTypeId, Guid TemplateId,
        Person Mine, Person Theirs, Guid Bystander, Guid MyMembershipId, Guid TheirMembershipId);

    private void SignIn(Person person)
    {
        CurrentUser.TenantId = _tenantId;
        CurrentUser.UserId = person.UserId;
        CurrentUser.IsAuthenticated = true;
    }

    private Guid _tenantId;

    private async Task<MemberMembership> LoadMembershipAsync(Guid membershipId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.MemberMemberships.AsNoTracking().SingleAsync(m => m.Id == membershipId);
    }

    private async Task<Guid> AddInvoiceAsync(World w, Guid memberId, string number, DateOnly issuedOn, decimal total)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var invoice = new Invoice
        {
            TenantId = w.TenantId, BranchId = w.BranchId, MemberId = memberId,
            InvoiceNumber = number, IssueDate = issuedOn, DueDate = issuedOn.AddDays(14),
            Status = InvoiceStatus.Issued, Subtotal = total, TotalAmount = total, Currency = "USD",
            CreatedAt = Wednesday
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task AddPaymentAsync(Guid invoiceId, decimal amount, PaymentStatus status)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.Payments.Add(new Payment
        {
            InvoiceId = invoiceId, Method = PaymentMethod.Card, Amount = amount,
            PaidAt = Wednesday.AddDays(-1), Status = status
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddSessionAsync(World w, DateTimeOffset startsAt)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var session = new ClassSession
        {
            TenantId = w.TenantId, BranchId = w.BranchId, ClassTypeId = w.ClassTypeId,
            StartsAt = startsAt, DurationMinutes = 45, Capacity = 2, Location = "Studio A"
        };
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task AddBookingAsync(World w, Guid sessionId, Guid memberId, ClassBookingStatus status, DateTimeOffset bookedAt)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.ClassBookings.Add(new ClassBooking
        {
            TenantId = w.TenantId, BranchId = w.BranchId, ClassSessionId = sessionId,
            MemberId = memberId, Status = status, BookedAt = bookedAt
        });
        await db.SaveChangesAsync();
    }

    private async Task AddNotificationAsync(World w, Guid memberId, DateTimeOffset scheduledFor)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.ScheduledNotifications.Add(new ScheduledNotification
        {
            TenantId = w.TenantId, BranchId = w.BranchId, NotificationTemplateId = w.TemplateId,
            RecipientMemberId = memberId, ScheduledFor = scheduledFor,
            Status = ScheduledNotificationStatus.Pending
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Two members with logins, in ONE tenant and ONE branch. Sharing the branch is the point: if
    /// they were separated by tenant, the global tenant filter would make every isolation assertion
    /// in this file pass without the portal doing anything at all.
    /// </summary>
    private async Task<World> SeedAsync(int maxFreezeDays = 14, bool autoRenew = false)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);
        _tenantId = tenant.Id;

        var branch = new Branch
        {
            TenantId = tenant.Id, Name = "Riverside", AddressLine = "1 Main St", City = "Portland", Country = "US"
        };
        db.Branches.Add(branch);

        db.GymProfiles.Add(new GymProfile
        {
            TenantId = tenant.Id, LegalName = "Titan Fitness LLC", DisplayName = "Titan Fitness",
            SupportEmail = "help@titan.example", SupportPhone = "+1-555-0100",
            DefaultCurrency = "USD", DefaultTimeZone = "UTC"
        });

        var plan = new MembershipPlan
        {
            TenantId = tenant.Id, Name = "Annual", Type = MembershipPlanType.Annual,
            DurationDays = 365, Price = 449.99m, Currency = "USD", MaxFreezeDays = maxFreezeDays
        };
        db.MembershipPlans.Add(plan);

        var classType = new ClassType { TenantId = tenant.Id, Name = "Strength Circuit", ColorHex = "#7c3aed" };
        db.ClassTypes.Add(classType);

        var template = new NotificationTemplate
        {
            TenantId = tenant.Id, Code = "membership-expiry", Category = NotificationCategory.MembershipExpiry,
            Channel = NotificationChannel.InApp, Subject = "Your membership is expiring",
            BodyTemplate = "Hi {{FirstName}}, renew to keep your access."
        };
        db.NotificationTemplates.Add(template);

        var mine = AddPerson(db, tenant.Id, branch.Id, "Mine");
        var theirs = AddPerson(db, tenant.Id, branch.Id, "Theirs");

        // A third member with no login at all — he only ever needs to occupy a place in a queue.
        var bystander = AddMember(db, tenant.Id, branch.Id, "Bystander", userId: null);

        var myMembership = AddMembership(db, mine.MemberId, plan.Id, autoRenew);
        var theirMembership = AddMembership(db, theirs.MemberId, plan.Id, autoRenew);

        await db.SaveChangesAsync();

        return new World(
            tenant.Id, branch.Id, classType.Id, template.Id, mine, theirs, bystander,
            myMembership, theirMembership);
    }

    private static Person AddPerson(GymOsDbContext db, Guid tenantId, Guid branchId, string name)
    {
        var user = new User
        {
            TenantId = tenantId, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = name, LastName = "Member"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });

        return new Person(AddMember(db, tenantId, branchId, name, user.Id), user.Id);
    }

    private static Guid AddMember(GymOsDbContext db, Guid tenantId, Guid branchId, string firstName, Guid? userId)
    {
        var member = new Member
        {
            TenantId = tenantId, BranchId = branchId, UserId = userId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = firstName, LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            Status = MemberStatus.Active, QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);
        return member.Id;
    }

    private static Guid AddMembership(GymOsDbContext db, Guid memberId, Guid planId, bool autoRenew)
    {
        var membership = new MemberMembership
        {
            MemberId = memberId, MembershipPlanId = planId,
            StartDate = Today.AddDays(-30), EndDate = Today.AddDays(335),
            Status = MemberMembershipStatus.Active, AutoRenew = autoRenew,
            PricePaid = 449.99m, Currency = "USD"
        };
        db.MemberMemberships.Add(membership);
        return membership.Id;
    }
}

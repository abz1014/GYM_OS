using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Domain.Attendance;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using GymOS.Domain.Trainers;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Queries;

/// <summary>
/// One member's history as a single chronology, instead of six modules a staff member has to visit
/// and reconcile in their head.
///
/// Staff do not decide from isolated records. They decide from a pattern — is this person coming in,
/// following the plan, paying, still in touch with their coach — and until now assembling that
/// pattern was manual work done from memory across Attendance, Workouts, Billing, Nutrition and
/// Memberships. The data was always there. Nothing here computes anything new; it only puts the
/// events in one order.
///
/// EVERY SOURCE IS GATED ON THE CALLER'S OWN MODULE PERMISSION. A timeline is exactly the shape of
/// thing that quietly becomes a permission bypass — one endpoint returning what six endpoints
/// individually refuse — so a receptionist without workouts.view gets a chronology with no training
/// in it, and a trainer without billing.view gets one with no money in it. Neither is told the other
/// exists.
///
/// Coach messages appear WITHOUT their body. That a member is still in contact with their coach is a
/// real engagement signal and belongs here; what was actually said belongs to the pairing, and no
/// permission in this product means "may read someone else's coaching thread".
///
/// Individual meal and water rows are deliberately absent. A chronology of two hundred snacks is not
/// a narrative — the diet plan is the event worth recording, and adherence already has a home on the
/// nutrition screen.
///
/// Each source is queried separately and merged in memory rather than unioned in SQL. That keeps
/// every query a plain Where/OrderBy/Take that any provider can translate, and it is what lets this
/// be tested on the SQLite harness at all.
/// </summary>
/// <param name="Take">
/// How many entries to return. Applied per source before the merge as well as after it, so a member
/// with a thousand check-ins cannot crowd out their own billing history.
/// </param>
public record GetMemberTimelineQuery(Guid MemberId, int Take = GetMemberTimelineQueryHandler.DefaultTake)
    : IQuery<IReadOnlyList<MemberTimelineEntryDto>>;

public class GetMemberTimelineQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMemberTimelineQuery, IReadOnlyList<MemberTimelineEntryDto>>
{
    public const int DefaultTake = 40;

    public const int MaxTake = 200;

    public async Task<IReadOnlyList<MemberTimelineEntryDto>> Handle(
        GetMemberTimelineQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, MaxTake);

        // Resolved through the global filters, so a member in a branch this caller cannot see is a
        // 404 rather than an empty timeline — an empty one would confirm the id exists.
        var memberExists = await db.Members.AsNoTracking()
            .AnyAsync(m => m.Id == request.MemberId, cancellationToken);

        if (!memberExists)
        {
            throw new NotFoundException(nameof(Member), request.MemberId);
        }

        var entries = new List<MemberTimelineEntryDto>();

        if (currentUser.HasPermission(PermissionCodes.Attendance.View))
        {
            /*
             * Ordered and trimmed in memory, not in SQL. SQLite refuses DateTimeOffset in both ORDER
             * BY and range filters, so the timestamped sources here cannot be narrowed server-side
             * without giving up the ability to test this query at all — the same trade GetMyCoachQuery
             * and GetMyClientsQuery already make, for the same reason.
             *
             * What bounds it is the member: this reads one person's own history, which in this product
             * is hundreds of rows, not millions. If a single member's attendance ever gets big enough
             * for that to hurt, the fix is a date-window column the provider can translate — not a
             * bigger Take.
             */
            var visits = await db.AttendanceRecords.AsNoTracking()
                .Where(a => a.MemberId == request.MemberId)
                .Select(a => new { a.CheckInAt, a.CheckOutAt, a.Method })
                .ToListAsync(cancellationToken);

            visits = visits.OrderByDescending(v => v.CheckInAt).Take(take).ToList();

            entries.AddRange(visits.Select(v => new MemberTimelineEntryDto(
                "Visit",
                v.CheckInAt,
                "Checked in",
                v.CheckOutAt is null
                    ? (v.Method == AttendanceMethod.Manual ? "Recorded at the desk" : "QR scan")
                    // A closed visit can say how long they stayed; an open one cannot, and guessing
                    // "still here" from a row nobody closed is how a member ends up in the building
                    // for a fortnight.
                    : $"Stayed {Math.Max((int)(v.CheckOutAt.Value - v.CheckInAt).TotalMinutes, 0)} min")));
        }

        if (currentUser.HasPermission(PermissionCodes.Workouts.View))
        {
            // In memory for the same reason as the visits above.
            var workouts = await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == request.MemberId)
                .Select(w => new
                {
                    w.LoggedAt,
                    TemplateName = w.WorkoutTemplate!.Name,
                    ExerciseCount = w.Entries.Count
                })
                .ToListAsync(cancellationToken);

            workouts = workouts.OrderByDescending(w => w.LoggedAt).Take(take).ToList();

            entries.AddRange(workouts.Select(w => new MemberTimelineEntryDto(
                "Workout",
                w.LoggedAt,
                w.TemplateName ?? "Workout logged",
                w.ExerciseCount == 1 ? "1 exercise" : $"{w.ExerciseCount} exercises")));
        }

        if (currentUser.HasPermission(PermissionCodes.Nutrition.View))
        {
            var plans = await db.DietPlans.AsNoTracking()
                .Where(p => p.MemberId == request.MemberId)
                .OrderByDescending(p => p.StartDate)
                .Take(take)
                .Select(p => new { p.Name, p.StartDate, p.TargetCalories })
                .ToListAsync(cancellationToken);

            entries.AddRange(plans.Select(p => new MemberTimelineEntryDto(
                "Nutrition",
                AsInstant(p.StartDate),
                $"Diet plan started · {p.Name}",
                p.TargetCalories is null ? null : $"{p.TargetCalories:0} kcal target")));
        }

        if (currentUser.HasPermission(PermissionCodes.Billing.View))
        {
            var invoices = await db.Invoices.AsNoTracking()
                .Where(i => i.MemberId == request.MemberId)
                .OrderByDescending(i => i.IssueDate)
                .Take(take)
                .Select(i => new { i.InvoiceNumber, i.IssueDate, i.Status, i.TotalAmount, i.Currency })
                .ToListAsync(cancellationToken);

            entries.AddRange(invoices.Select(i => new MemberTimelineEntryDto(
                "Invoice",
                AsInstant(i.IssueDate),
                $"Invoice {i.InvoiceNumber} · {i.Status}",
                $"{i.TotalAmount:0.00} {i.Currency}")));

            var payments = await db.Payments.AsNoTracking()
                .Where(p => p.Invoice!.MemberId == request.MemberId && p.Status == PaymentStatus.Completed)
                .Select(p => new { p.PaidAt, p.Amount, p.Method, Currency = p.Invoice!.Currency })
                .ToListAsync(cancellationToken);

            payments = payments.OrderByDescending(p => p.PaidAt).Take(take).ToList();

            entries.AddRange(payments.Select(p => new MemberTimelineEntryDto(
                "Payment",
                p.PaidAt,
                "Payment received",
                $"{p.Amount:0.00} {p.Currency} · {p.Method}")));
        }

        if (currentUser.HasPermission(PermissionCodes.Memberships.View))
        {
            var memberships = await db.MemberMemberships.AsNoTracking()
                .Where(mm => mm.MemberId == request.MemberId)
                .OrderByDescending(mm => mm.StartDate)
                .Take(take)
                .Select(mm => new { PlanName = mm.MembershipPlan!.Name, mm.StartDate, mm.EndDate, mm.Status })
                .ToListAsync(cancellationToken);

            entries.AddRange(memberships.Select(mm => new MemberTimelineEntryDto(
                "Membership",
                AsInstant(mm.StartDate),
                $"{mm.PlanName} started",
                $"Runs to {mm.EndDate:d MMM yyyy} · {mm.Status}")));
        }

        // Measurements ride on members.view, which every caller reaching this endpoint already holds
        // — they are part of the member record rather than a module of their own, and the panel has
        // always shown them under members.view.
        var measurements = await db.MemberMeasurements.AsNoTracking()
            .Where(m => m.MemberId == request.MemberId)
            .OrderByDescending(m => m.MeasuredOn)
            .Take(take)
            .Select(m => new { m.MeasuredOn, m.WeightKg, m.BodyFatPercentage })
            .ToListAsync(cancellationToken);

        entries.AddRange(measurements.Select(m => new MemberTimelineEntryDto(
            "Measurement",
            AsInstant(m.MeasuredOn),
            "Measurement recorded",
            Describe(m.WeightKg, m.BodyFatPercentage))));

        if (currentUser.HasPermission(PermissionCodes.Trainers.View))
        {
            var messages = await db.CoachMessages.AsNoTracking()
                .Where(c => c.MemberId == request.MemberId)
                .Select(c => new { c.SentAt, c.Author })
                .ToListAsync(cancellationToken);

            messages = messages.OrderByDescending(c => c.SentAt).Take(take).ToList();

            entries.AddRange(messages.Select(c => new MemberTimelineEntryDto(
                "Coaching",
                c.SentAt,
                c.Author == CoachMessageAuthor.Trainer ? "Coach wrote to them" : "They wrote to their coach",
                // No body, on purpose — see the remarks on this class.
                null)));
        }

        return entries
            .OrderByDescending(e => e.At)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// A calendar date as an instant, at midnight UTC. These sources genuinely have no time of day;
    /// inventing one would order them confidently and wrongly against the timestamped events.
    /// </summary>
    private static DateTimeOffset AsInstant(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string? Describe(decimal? weightKg, decimal? bodyFat)
    {
        var parts = new List<string>(2);
        if (weightKg is not null) parts.Add($"{weightKg:0.#} kg");
        if (bodyFat is not null) parts.Add($"{bodyFat:0.#}% body fat");
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}

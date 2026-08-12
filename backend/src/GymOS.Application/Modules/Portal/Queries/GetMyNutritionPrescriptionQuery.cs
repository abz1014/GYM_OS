using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Common;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// What the nutritionist prescribed, what changes this week and this month, and whether the member
/// has confirmed today. Nothing about what they ate.
///
/// The member's nutrition screen was four macro bars whose "consumed" halves came from logged meals,
/// a water card, and three buttons that all navigated to the logging form. For a member who does not
/// keep a food diary — which is nearly all of them — that is a screen of permanent zeroes against
/// real targets, reading as total failure rather than as "not tracked". The owner's word for it was
/// "useless" and the diagnosis was right: a nutrition plan is something you are GIVEN, and the app
/// was only ever asking.
///
/// So this returns the prescription. Targets, the standing instructions, the current weekly and
/// monthly guidance, when the plan is next reviewed, and the recent history of what changed — which
/// is the part that makes a plan feel like it is progressing rather than sitting there.
///
/// The one thing it asks for is a tap: <see cref="MyNutritionPrescriptionDto.ConfirmedToday"/>, so
/// the screen knows whether to offer the confirmation or acknowledge it. See
/// LogMyPlanAdherenceCommand for why that tap exists at all.
/// </summary>
public record GetMyNutritionPrescriptionQuery : IQuery<MyNutritionPrescriptionDto>;

public class GetMyNutritionPrescriptionQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyNutritionPrescriptionQuery, MyNutritionPrescriptionDto>
{
    /// <summary>Days of guidance history the screen shows. Enough to see the plan moving.</summary>
    private const int HistoryDays = 60;

    /// <summary>Days of adherence the streak line is drawn from — four weeks, like the rank pace.</summary>
    private const int AdherenceWindowDays = 28;

    public async Task<MyNutritionPrescriptionDto> Handle(
        GetMyNutritionPrescriptionQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var plan = (await db.DietPlans.AsNoTracking()
                .Where(p => p.MemberId == memberId && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .Select(p => new
                {
                    p.Id, p.Name, p.Notes, p.StartDate, p.EndDate,
                    p.TargetCalories, p.TargetProteinG, p.TargetCarbsG, p.TargetFatG, p.CreatedByUserId
                })
                .ToListAsync(cancellationToken))
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefault();

        if (plan is null)
        {
            // No plan is a real state with its own screen, not an error and not an empty plan.
            return new MyNutritionPrescriptionDto(
                null, null, null, null, null, null, null, null, null, null, null, [], false, 0,
                AdherenceWindowDays);
        }

        var author = plan.CreatedByUserId is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(u => u.Id == plan.CreatedByUserId)
                .Select(u => $"{u.FirstName} {u.LastName}".Trim())
                .FirstOrDefaultAsync(cancellationToken);

        var guidance = await db.DietPlanGuidance.AsNoTracking()
            .Where(g => g.DietPlanId == plan.Id && g.EffectiveFrom <= today && g.EffectiveFrom >= today.AddDays(-HistoryDays))
            .Select(g => new { g.Cadence, g.EffectiveFrom, g.Title, g.Body })
            .ToListAsync(cancellationToken);

        // The current note per cadence is the newest one whose date has arrived — so a nutritionist
        // can write next Monday's on Friday without it appearing early.
        MyGuidanceDto? Current(GuidanceCadence cadence) => guidance
            .Where(g => g.Cadence == cadence)
            .OrderByDescending(g => g.EffectiveFrom)
            .Select(g => new MyGuidanceDto(g.Cadence.ToString(), g.EffectiveFrom, g.Title, g.Body))
            .FirstOrDefault();

        var thisWeek = Current(GuidanceCadence.Weekly);
        var thisMonth = Current(GuidanceCadence.Monthly);

        // Everything the member has been told, newest first, minus the two shown above it — the
        // history is what makes a plan read as progressing rather than static.
        var history = guidance
            .OrderByDescending(g => g.EffectiveFrom)
            .Select(g => new MyGuidanceDto(g.Cadence.ToString(), g.EffectiveFrom, g.Title, g.Body))
            .Where(g => g != thisWeek && g != thisMonth)
            .ToList();

        var windowStart = today.AddDays(-AdherenceWindowDays);
        var adherenceDates = await db.PlanAdherenceLogs.AsNoTracking()
            .Where(a => a.MemberId == memberId && a.OnDate >= windowStart)
            .Select(a => a.OnDate)
            .ToListAsync(cancellationToken);

        return new MyNutritionPrescriptionDto(
            plan.Name,
            author,
            plan.Notes,
            plan.StartDate,
            plan.EndDate,
            plan.TargetCalories,
            plan.TargetProteinG,
            plan.TargetCarbsG,
            plan.TargetFatG,
            thisWeek,
            thisMonth,
            history,
            adherenceDates.Contains(today),
            adherenceDates.Distinct().Count(),
            AdherenceWindowDays);
    }
}

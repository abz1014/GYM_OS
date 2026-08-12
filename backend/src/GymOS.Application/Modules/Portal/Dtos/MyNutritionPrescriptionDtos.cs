namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>
/// One instruction from the nutritionist, and when it started applying.
/// </summary>
/// <param name="Cadence">"Weekly" or "Monthly" — how often this kind of note is replaced.</param>
public record MyGuidanceDto(string Cadence, DateOnly EffectiveFrom, string Title, string? Body);

/// <summary>
/// The member's nutrition plan as something to follow, not something to fill in.
///
/// Every field is null when the member has no active plan, which is a real state with its own
/// screen — not an error, and not an empty plan with zeroes in it. See GetMyNutritionPrescriptionQuery.
/// </summary>
/// <param name="AuthoredBy">The nutritionist's name, so advice has a person attached to it.</param>
/// <param name="Notes">Standing instructions. The plan had nowhere for words at all before this.</param>
/// <param name="EndDate">When the plan is next reviewed. Null means it runs until replaced.</param>
/// <param name="ThisWeek">The current weekly note, or null if the nutritionist has not written one.</param>
/// <param name="ThisMonth">The current monthly note.</param>
/// <param name="History">Everything else the member has been told recently, newest first — the part
/// that makes a plan read as progressing rather than static.</param>
/// <param name="ConfirmedToday">Whether they have already tapped "stayed on plan" today, so the
/// screen acknowledges it rather than offering a button that would silently do nothing.</param>
/// <param name="DaysConfirmedInWindow">Days confirmed out of <paramref name="AdherenceWindowDays"/>.</param>
public record MyNutritionPrescriptionDto(
    string? PlanName,
    string? AuthoredBy,
    string? Notes,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? TargetCalories,
    decimal? TargetProteinG,
    decimal? TargetCarbsG,
    decimal? TargetFatG,
    MyGuidanceDto? ThisWeek,
    MyGuidanceDto? ThisMonth,
    IReadOnlyList<MyGuidanceDto> History,
    bool ConfirmedToday,
    int DaysConfirmedInWindow,
    int AdherenceWindowDays);

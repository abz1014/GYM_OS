namespace GymOS.Application.Modules.Nutrition.Dtos;

/// <summary>One member a nutritionist is responsible for, and whether that work is still current.</summary>
/// <param name="PlanIsActive">
/// False when the plan has an end date that has passed. A lapsed plan is the nutritionist's own
/// backlog — the member is still theirs, the prescription simply ran out — so those rows stay in the
/// roster rather than dropping out of it, which is how someone quietly stops being anyone's client.
/// </param>
/// <param name="LastMealLoggedAt">
/// Null when the member has never logged against this plan. Distinct from "logged nothing this week":
/// one is a member who never started, the other is one who stopped.
/// </param>
public record NutritionClientRowDto(
    Guid MemberId,
    string MemberName,
    string MemberCode,
    string PlanName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool PlanIsActive,
    DateTimeOffset? LastMealLoggedAt,
    int MealsLoggedLast7Days);

/// <param name="ActiveMembersWithoutAPlan">
/// How many active members hold no current plan from anyone. The other half of a nutritionist's
/// question: the roster answers "who are mine", this answers "how much is unserved". A number rather
/// than a list on purpose — 300 members with no plan is not a worklist, and pretending otherwise
/// would just be the member directory wearing a different hat.
/// </param>
public record MyNutritionClientsDto(
    IReadOnlyList<NutritionClientRowDto> Clients,
    int ActiveMembersWithoutAPlan);

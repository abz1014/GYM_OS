using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

/// <summary>
/// Raised when a member confirms they stayed on their plan for a day.
///
/// Carries exactly what <see cref="MealLoggedEvent"/> carries, and for the same reason: the award is
/// keyed per day, not per action. That symmetry is load-bearing rather than tidy — both events feed
/// the same XpReason through the same derived day key, so a member who both ticks adherence AND logs
/// a meal on the same day earns the fifteen once, not twice.
/// </summary>
public record PlanAdherenceLoggedEvent(Guid MemberId, DateOnly OnDate) : DomainEvent;

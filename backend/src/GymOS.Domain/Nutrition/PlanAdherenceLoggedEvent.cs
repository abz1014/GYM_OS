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
/// <param name="XpDayUtc">
/// The UTC calendar day, NOT the gym-clock day the row is stored under.
///
/// This is the subtle half of the symmetry and it was wrong on the first pass. AddMealEntryCommand
/// keys its event on <c>DateOnly.FromDateTime(consumedAt.UtcDateTime)</c>; the adherence command
/// resolves the member's gym timezone and was keying on that. Both then hash
/// "{member}:nutrition:{date}" — so for any gym not on UTC, a tick and a meal logged the same
/// evening produced two DIFFERENT strings, the idempotency check missed, and the member was paid
/// thirty for one day.
///
/// The dedupe test did not catch it because the harness runs in UTC, where the two clocks agree.
/// Matching the existing UTC keying is the conservative fix: it changes nothing about what anyone
/// has already earned, whereas moving BOTH paths onto the gym clock would hand a second award to
/// every member who had already logged a meal on the transition day.
/// </param>
public record PlanAdherenceLoggedEvent(Guid MemberId, DateOnly XpDayUtc) : DomainEvent;

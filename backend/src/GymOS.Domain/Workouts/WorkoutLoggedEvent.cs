using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

/// <summary>Raised when a member logs a workout. Consumed by the Member Experience Engine (XP award,
/// mastery/PR updates in later slices). Carries only ids — handlers load whatever detail they need.</summary>
public record WorkoutLoggedEvent(Guid MemberId, Guid WorkoutLogId) : DomainEvent;

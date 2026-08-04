using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

/// <summary>Raised when a member logs a meal against a diet plan. Consumed by the Member Experience
/// Engine to reward nutrition consistency (once per consumed day). Carries the consumed date so the
/// award is keyed per day rather than per meal — logging six snacks earns the same as one square
/// meal.</summary>
public record MealLoggedEvent(Guid MemberId, DateOnly ConsumedDate) : DomainEvent;

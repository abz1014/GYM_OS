using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>Raised when a member logs a rest / recovery day. Consumed by the Member Experience Engine
/// to reward recovery consistency (once per logged day). Carries the logged date so the award is keyed
/// per day — logging two recovery activities on the same day earns the reward once.</summary>
public record RecoveryLoggedEvent(Guid MemberId, DateOnly LoggedDate) : DomainEvent;

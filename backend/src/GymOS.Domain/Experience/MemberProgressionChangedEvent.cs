using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// Raised when a member's progression changes (XP awarded / level recomputed). It's the "member
/// advanced" signal the rest of the engine hangs derived work off — Slice 3's achievement evaluation
/// consumes it in a SECOND dispatch pass, by which point the XP/PR/mastery writes from the first pass
/// are committed, so evaluation reads fresh state regardless of sibling-handler order.
/// </summary>
public record MemberProgressionChangedEvent(Guid MemberId) : DomainEvent;

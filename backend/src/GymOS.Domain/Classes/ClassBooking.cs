using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Classes;

/// <summary>
/// A member's place in a specific ClassSession. Its Status is a small state machine (see
/// ClassBookingStatus): a booking is Booked or Waitlisted at creation depending on capacity, moves
/// to CheckedIn/NoShow around the session, or Cancelled if released — and a released confirmed spot
/// can promote the earliest Waitlisted booking. Branch-scoped (denormalised from the session) so it
/// participates in the same branch-access filtering as every other operational record.
/// </summary>
public class ClassBooking : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ClassSessionId { get; set; }

    public ClassSession? ClassSession { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public ClassBookingStatus Status { get; set; }

    public DateTimeOffset BookedAt { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}

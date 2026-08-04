using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Attendance;

public class AttendanceRecord : AggregateRoot, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public DateTimeOffset CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public AttendanceMethod Method { get; set; }

    public Guid? RecordedByUserId { get; set; }

    /// <summary>Signals the Member Experience Engine that a member checked in. Raised only by the
    /// live CheckInCommand — the historical Migration Center import deliberately does NOT call this,
    /// so backfilling attendance never awards XP for visits that happened before the member joined
    /// the experience system.</summary>
    public void RaiseCheckedIn() => AddDomainEvent(new MemberCheckedInEvent(MemberId, Id));
}

using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Attendance;

public class AttendanceRecord : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public DateTimeOffset CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public AttendanceMethod Method { get; set; }

    public Guid? RecordedByUserId { get; set; }
}

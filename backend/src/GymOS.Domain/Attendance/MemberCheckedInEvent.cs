using GymOS.Domain.Common;

namespace GymOS.Domain.Attendance;

/// <summary>Raised when a member checks in. Consumed by the Member Experience Engine (visit XP,
/// attendance streaks in later slices).</summary>
public record MemberCheckedInEvent(Guid MemberId, Guid AttendanceRecordId) : DomainEvent;

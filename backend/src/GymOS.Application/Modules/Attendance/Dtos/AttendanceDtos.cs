using GymOS.Domain.Attendance;

namespace GymOS.Application.Modules.Attendance.Dtos;

public record AttendanceRecordDto(
    Guid Id, Guid MemberId, string MemberName, DateTimeOffset CheckInAt, DateTimeOffset? CheckOutAt, AttendanceMethod Method);

public record PeakHourBucketDto(int HourOfDay, int CheckInCount);

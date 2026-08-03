namespace GymOS.Application.Modules.Trainers.Dtos;

public record TrainerListItemDto(
    Guid Id, string FullName, string Email, string Specialties, decimal CommissionRate, bool IsActive,
    int ActiveClientCount, double? AverageRating);

public record TrainerScheduleDto(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable);

public record TrainerAssignmentDto(Guid Id, Guid MemberId, string MemberName, DateOnly StartDate, DateOnly? EndDate, bool IsActive);

public record TrainerSessionDto(
    Guid Id, Guid TrainerAssignmentId, Guid MemberId, string MemberName, DateTimeOffset ScheduledAt,
    int DurationMinutes, string Status, string? Notes, DateTimeOffset? CompletedAt);

public record TrainerRatingDto(Guid Id, Guid MemberId, string MemberName, int Score, string? Comment, DateTimeOffset RatedAt, Guid? SessionId);

public record CommissionRecordDto(Guid Id, decimal Amount, DateOnly Period, string Status);

public record TrainerDetailDto(
    Guid Id, string FirstName, string LastName, string Email, string Specialties, decimal CommissionRate,
    string? Bio, bool IsActive, Guid BranchId,
    IReadOnlyList<TrainerScheduleDto> Schedules,
    IReadOnlyList<TrainerAssignmentDto> Assignments,
    IReadOnlyList<TrainerSessionDto> Sessions,
    IReadOnlyList<TrainerRatingDto> Ratings,
    IReadOnlyList<CommissionRecordDto> CommissionRecords,
    decimal TotalCommissionEarned);

public record CreateTrainerResultDto(Guid TrainerId, string TemporaryPassword);

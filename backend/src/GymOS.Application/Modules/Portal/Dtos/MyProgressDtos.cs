namespace GymOS.Application.Modules.Portal.Dtos;

public record MyWeightPointDto(DateOnly MeasuredOn, decimal WeightKg);

public record MyGoalDto(Guid Id, string Title, DateOnly? TargetDate, bool IsAchieved, DateTimeOffset? AchievedAt);

public record MyProgressPhotoDto(Guid Id, string PhotoUrl, DateTimeOffset TakenAt, string? Notes);

public record MyProgressDto(
    int WeeklyStreak,
    int TotalVisits,
    int VisitsThisMonth,
    IReadOnlyList<MyWeightPointDto> WeightTrend,
    IReadOnlyList<MyGoalDto> Goals,
    IReadOnlyList<MyProgressPhotoDto> Photos);

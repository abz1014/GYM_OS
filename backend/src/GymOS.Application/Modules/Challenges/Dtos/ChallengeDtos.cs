namespace GymOS.Application.Modules.Challenges.Dtos;

/// <summary>A challenge as the member sees it — the catalog entry plus their own participation
/// status and progress toward its goal. Unjoined challenges are included too (so the member can see
/// what's available), matching the achievement shelf's "show locked ones too" convention.</summary>
public record MyChallengeDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    int TargetWorkoutCount,
    bool Joined,
    bool IsCompleted,
    int MyWorkoutCount);

/// <summary>A challenge as staff manage it — the catalog entry plus aggregate participation counts.</summary>
public record ChallengeListItemDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? BranchId,
    DateOnly StartDate,
    DateOnly EndDate,
    int TargetWorkoutCount,
    int ParticipantCount,
    int CompletedCount);

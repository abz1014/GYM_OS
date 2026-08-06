namespace GymOS.Application.Modules.Coaching.Dtos;

/// <summary>One member's plateaued exercise — held identical weight/reps for two sessions running,
/// the same signal the member's own "add weight next time" card shows, surfaced across a trainer's
/// whole roster.</summary>
public record PlateauRowDto(
    Guid MemberId,
    string MemberName,
    string MemberCode,
    Guid ExerciseId,
    string ExerciseName,
    decimal? LastWeightKg,
    decimal? SuggestedNextWeightKg,
    DateTimeOffset LastLoggedAt);

/// <summary>One member's workout/nutrition consistency over the trailing window. NutritionAdherencePercent
/// is null when the member has never had a diet plan — nothing to be compliant with yet.</summary>
public record ComplianceRowDto(
    Guid MemberId,
    string MemberName,
    string MemberCode,
    int WorkoutAdherencePercent,
    int? NutritionAdherencePercent,
    DateTimeOffset? LastWorkoutAt,
    DateTimeOffset? LastMealLoggedAt);

/// <summary>One member flagged for the trainer to check in on — either an overtraining signal from
/// RecoveryPolicy or a weekly streak about to lapse from CoachingPolicy.</summary>
public record RiskRowDto(
    Guid MemberId,
    string MemberName,
    string MemberCode,
    string RiskType,
    string Reason);

/// <summary>One client's conversation as their trainer sees it. Mirrors MyCoachDto on the member side.</summary>
public record CoachConversationDto(
    Guid MemberId,
    string MemberName,
    bool CanSend,
    int UnreadCount,
    bool HasOlder,
    IReadOnlyList<CoachMessageDto> Messages);

/// <param name="Author">"Member" or "Trainer".</param>
public record CoachMessageDto(
    Guid Id, string Author, string Body, DateTimeOffset SentAt, bool Read, Guid? WorkoutLogId);

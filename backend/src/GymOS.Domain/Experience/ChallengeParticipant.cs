using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A member's opt-in join to a <see cref="CommunityChallenge"/>. Not tenant-scoped itself (mirrors
/// WorkoutTemplateExercise/SkillNode — scoped through its parent); the query surface always resolves
/// the caller's own MemberId first, which is already tenant-correct. IsCompleted flips exactly once
/// (set by EvaluateChallengeProgressOnWorkoutLoggedHandler when the member's workout count in the
/// challenge's date window reaches its target), which is what makes the challenge-XP award idempotent.
/// </summary>
public class ChallengeParticipant : BaseEntity
{
    public Guid ChallengeId { get; set; }

    public CommunityChallenge? Challenge { get; set; }

    public Guid MemberId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

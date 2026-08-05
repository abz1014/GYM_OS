namespace GymOS.Domain.Experience;

/// <summary>
/// One coaching nudge, always self-explaining ("always explain" — blueprint Phase 6): the Explanation
/// is never omitted, so the member can see WHY, not just what. A read-model value, never persisted —
/// recommendations are recomputed from current state on every request, same as RecoveryStatus.
/// </summary>
public record Recommendation(RecommendationType Type, string Title, string Explanation, Guid? ExerciseId = null);

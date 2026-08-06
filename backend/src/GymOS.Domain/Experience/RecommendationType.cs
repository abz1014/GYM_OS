namespace GymOS.Domain.Experience;

/// <summary>The kind of coaching nudge a <see cref="Recommendation"/> carries — lets the frontend
/// pick an icon/tone without parsing the explanation text.</summary>
public enum RecommendationType
{
    /// <summary>This week's training volume moved sharply versus last week (up or down).</summary>
    VolumeSuggestion,

    /// <summary>A skill-tree exercise the member is ready to progress to, given what they've already mastered.</summary>
    ExerciseSubstitution,

    /// <summary>Recovery status needs attention (Fatigued/OvertrainingRisk) — surfaced only when action is warranted.</summary>
    RecoveryAdvice,

    /// <summary>A trainer has an active workout plan assigned — defer to it instead of self-directed suggestions.</summary>
    TrainerPlanActive
}

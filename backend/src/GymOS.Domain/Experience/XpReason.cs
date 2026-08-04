namespace GymOS.Domain.Experience;

/// <summary>
/// Why a member earned XP. The award weights (see <see cref="XpPolicy"/>) deliberately favour
/// consistency, verification, and goal completion over raw load — the blueprint's rule is "never
/// reward unsafe lifting alone". Slice 1 uses only <see cref="WorkoutCompleted"/> and
/// <see cref="GymVisit"/>; the rest are defined up front so later slices award against a stable enum
/// (append new values at the end to keep stored integer values stable).
/// </summary>
public enum XpReason
{
    WorkoutCompleted,
    GymVisit,
    StreakMilestone,
    ProgressiveImprovement,
    GoalCompleted,
    TrainerVerified,
    NutritionAdherence,
    RecoveryLogged,
    ChallengeCompleted
}

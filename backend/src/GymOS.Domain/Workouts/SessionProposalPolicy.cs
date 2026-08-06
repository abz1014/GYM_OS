namespace GymOS.Domain.Workouts;

/// <summary>Where a proposed session came from. Drives what the member is asked to confirm.</summary>
public enum SessionProposalSource
{
    /// <summary>Nothing to propose — no plan, no history, no catalogue.</summary>
    None,
    /// <summary>Today's session from the member's active trainer plan.</summary>
    TrainerPlan,
    /// <summary>What they did last time. The majority path.</summary>
    RepeatLast,
    /// <summary>A short starter session for someone with no history at all.</summary>
    Starter
}

/// <summary>One movement in a proposed session, already filled in and ready to confirm.</summary>
public record ProposedEntry(Guid ExerciseId, string ExerciseName, int Sets, int Reps, decimal? WeightKg);

/// <summary>An exercise a trainer's plan prescribes. Carries no load — templates never store weight.</summary>
public record PlannedExercise(Guid ExerciseId, string ExerciseName, int Sets, int Reps);

/// <summary>A complete session the member can accept with one tap.</summary>
public record SessionProposal(SessionProposalSource Source, IReadOnlyList<ProposedEntry> Entries)
{
    public bool CanConfirm => Entries.Count > 0;
}

/// <summary>
/// What the app believes a member just did, so confirming it takes one tap instead of a form.
///
/// The rule this encodes: a member should never be asked to type something the system can reasonably
/// infer. Nothing here invents data — every number comes from either the trainer's prescription or
/// what the member actually lifted last time — but between them they cover a whole session, leaving
/// the member to confirm rather than compose.
///
/// Ordering matters and is deliberate:
///
/// 1. <see cref="SessionProposalSource.TrainerPlan"/> wins when one is active, because a member with
///    a programme is there to do the programme.
/// 2. <see cref="SessionProposalSource.RepeatLast"/> otherwise. This is the MAJORITY path, not a
///    fallback — only a minority of gym members have a trainer, so most people's best available
///    prediction is what they did last time.
/// 3. <see cref="SessionProposalSource.Starter"/> for someone with neither, so a first session is
///    still one tap rather than the one moment we hand a new member an empty form.
///
/// Weight is the piece a plan cannot supply: WorkoutTemplateExercise stores sets and reps but never
/// load. It comes from what the member last lifted on that movement, which is exactly the memory a
/// coach would bring. Where there is no history the entry stays null — a genuinely unknown load is
/// left for the member rather than guessed at, and bodyweight movements have no load to state.
/// </summary>
public static class SessionProposalPolicy
{
    /// <summary>Movements in a starter session — enough to be a workout, few enough to finish.</summary>
    public const int StarterExerciseCount = 3;

    public static SessionProposal Propose(
        IReadOnlyList<PlannedExercise> todaysPlan,
        IReadOnlyList<ProposedEntry> lastSession,
        IReadOnlyDictionary<Guid, decimal> lastWeightByExercise,
        IReadOnlyList<PlannedExercise> starterCatalogue)
    {
        if (todaysPlan.Count > 0)
        {
            return new SessionProposal(SessionProposalSource.TrainerPlan, Fill(todaysPlan, lastWeightByExercise));
        }

        if (lastSession.Count > 0)
        {
            // Already carries real loads — the member lifted them. Nothing to fill in.
            return new SessionProposal(SessionProposalSource.RepeatLast, lastSession);
        }

        if (starterCatalogue.Count > 0)
        {
            return new SessionProposal(
                SessionProposalSource.Starter,
                Fill(starterCatalogue.Take(StarterExerciseCount).ToList(), lastWeightByExercise));
        }

        return new SessionProposal(SessionProposalSource.None, []);
    }

    /// <summary>Attaches remembered loads to prescribed sets and reps.</summary>
    private static List<ProposedEntry> Fill(
        IReadOnlyList<PlannedExercise> planned, IReadOnlyDictionary<Guid, decimal> lastWeightByExercise)
        => planned
            .Select(p => new ProposedEntry(
                p.ExerciseId,
                p.ExerciseName,
                p.Sets,
                p.Reps,
                lastWeightByExercise.TryGetValue(p.ExerciseId, out var weight) ? weight : null))
            .ToList();
}

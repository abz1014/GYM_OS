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
/// <param name="LoadType">What this movement is measured in. The session screen renders its input
/// fields from this, so a proposal without it put KG and REPS columns on a treadmill — and the
/// confirm then sent a rep count the write path rejects. A proposal that cannot be confirmed as
/// proposed is worse than none.</param>
/// <param name="Reps">Null for a movement that has none — a run, a plank. Proposing a rep count
/// for one is how the fabricated 8 became self-perpetuating: the proposal re-served it, the member
/// confirmed it, and it came back as their own history.</param>
/// <param name="DurationSeconds">Last time's duration, for movements measured in time. Carried for
/// the same reason a load is: "same as last time" that forgets the 30 minutes you ran is a repeat
/// of the movement, not of the session.</param>
/// <param name="DistanceMeters">Last time's distance, for movements measured in it.</param>
public record ProposedEntry(
    Guid ExerciseId,
    string ExerciseName,
    ExerciseLoadType LoadType,
    int Sets,
    int? Reps,
    decimal? WeightKg,
    int? DurationSeconds,
    decimal? DistanceMeters);

/// <summary>An exercise a trainer's plan prescribes. Carries no load — templates never store weight.</summary>
public record PlannedExercise(Guid ExerciseId, string ExerciseName, ExerciseLoadType LoadType, int Sets, int Reps);

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

    /// <summary>
    /// The movements a first session is built from, most important first.
    ///
    /// Named rather than "the first three in the catalogue". That shortcut worked only while the
    /// catalogue held fifteen movements and alphabetical order happened to start at Barbell Squat;
    /// against a real catalogue it opens a beginner's first ever workout with Ab Wheel Rollout,
    /// Arnold Press and Barbell Curl — three movements that share no purpose and skip both legs and
    /// back. A push, a pull and a squat is the oldest full-body template there is.
    ///
    /// A gym whose catalogue has none of these still gets a session: the query falls back to what it
    /// has. The names are matched, not required.
    /// </summary>
    public static readonly IReadOnlyList<string> StarterExerciseNames =
        ["Barbell Squat", "Bench Press", "Bent-Over Row"];

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

    /// <summary>
    /// Attaches remembered loads to prescribed sets and reps — but only the measurements the
    /// movement actually has.
    ///
    /// A template stores SetsCount and RepsCount for EVERYTHING, including movements that have no
    /// reps: the seeded "Beginner Full Body" prescribes "Plank: 3×30", where the 30 almost certainly
    /// means seconds. Almost certainly is not a unit. Passing it through as reps re-creates the
    /// fabrication this whole area exists to remove (and the write path now rejects it); converting
    /// it to seconds would be inventing semantics the template never declared. So a no-rep movement
    /// is proposed as bare sets and the member supplies the measurement when they do it — honest,
    /// and confirmable as proposed.
    ///
    /// A remembered load is attached only where the movement can carry one: Weighted always, and
    /// Distance because a farmer's carry is measured in distance AND load. Bodyweight and Timed
    /// movements carry none, which is the same rule the write guard enforces.
    /// </summary>
    private static List<ProposedEntry> Fill(
        IReadOnlyList<PlannedExercise> planned, IReadOnlyDictionary<Guid, decimal> lastWeightByExercise)
        => planned
            .Select(p => new ProposedEntry(
                p.ExerciseId,
                p.ExerciseName,
                p.LoadType,
                p.Sets,
                p.LoadType is ExerciseLoadType.Weighted or ExerciseLoadType.Bodyweight ? p.Reps : null,
                p.LoadType is ExerciseLoadType.Weighted or ExerciseLoadType.Distance
                    && lastWeightByExercise.TryGetValue(p.ExerciseId, out var weight)
                    ? weight
                    : null,
                DurationSeconds: null,
                DistanceMeters: null))
            .ToList();
}

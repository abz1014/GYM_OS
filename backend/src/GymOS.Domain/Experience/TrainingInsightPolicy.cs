namespace GymOS.Domain.Experience;

/// <summary>What kind of thing the engine has noticed. Ordered by how actionable it is today.</summary>
public enum TrainingInsightKind
{
    /// <summary>A muscle group that needs rest — the only insight that says "not today".</summary>
    RecoveryAlert,

    /// <summary>A lift where the member has earned a heavier attempt.</summary>
    ReadyForPr,

    /// <summary>Something dropped that also happens to be their least-trained area.</summary>
    Comeback,

    /// <summary>A lift that has not moved in weeks.</summary>
    Plateau,

    /// <summary>Something they used to do and stopped.</summary>
    GoneQuiet,

    /// <summary>A lift that has genuinely gone up, stated as the number it went up by.</summary>
    Momentum
}

/// <summary>One thing worth telling the member, in their words.</summary>
public readonly record struct TrainingInsight(TrainingInsightKind Kind, string Title, string Detail);

/// <summary>A movement the member has stopped doing.</summary>
public readonly record struct QuietMovement(string ExerciseName, string? MuscleGroup, int DaysSince);

/// <summary>
/// Everything the engine already knows, gathered in one place. Every field is a fact another policy
/// computed; nothing here is inferred at this layer.
/// </summary>
/// <param name="MomentumGainKg">How much heavier, not how impressive. There is no distribution of
/// other members' progress to call a gain "unusual" against, and comparing a beginner's rate to an
/// advanced lifter's would say more about training age than effort.</param>
public readonly record struct TrainingInsightSignals(
    string? FatiguedMuscleGroup,
    string? RecoveryReason,
    string? ReadyExerciseName,
    decimal? ReadyNextWeightKg,
    string? PlateauExerciseName,
    IReadOnlyList<QuietMovement> GoneQuiet,
    string? WeakestMuscleGroup,
    string? MomentumExerciseName,
    decimal MomentumGainKg,
    int MomentumWeeks);

/// <summary>
/// The one or two things worth saying to a member today.
///
/// The engine behind this was already here, scattered: recovery classified muscle groups, the
/// overload policy knew who had earned a heavier attempt, mastery knew the weakest area, the
/// passport knew what had been dropped. Each fed a different screen, none ranked against the others,
/// and the home screen picked whichever happened to be wired to it. A member does not want a report
/// with six sections; they want the next useful thing.
///
/// Ranked by what a member can act on TODAY rather than by severity. Recovery leads because it is
/// the only one that changes what they should not do; a plateau is real but it will still be real
/// next week.
///
/// Two rules keep it honest. Nothing appears unless the underlying fact is present — an absent
/// signal produces no insight rather than a hedge. And nothing is described in terms the data cannot
/// support: a gain is reported as the weight it gained, never as "unusually good", because there is
/// no distribution here to call anything unusual against.
/// </summary>
public static class TrainingInsightPolicy
{
    /// <summary>Two at most. A third is a report, and nobody reads a report before training.</summary>
    public const int MaxInsights = 2;

    public static IReadOnlyList<TrainingInsight> Rank(TrainingInsightSignals s)
    {
        var insights = new List<TrainingInsight>();

        if (s.FatiguedMuscleGroup is { Length: > 0 } fatigued)
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.RecoveryAlert,
                $"{fatigued} needs a rest",
                string.IsNullOrWhiteSpace(s.RecoveryReason) ? "Train something else today." : s.RecoveryReason!));
        }

        if (s.ReadyExerciseName is { Length: > 0 } ready)
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.ReadyForPr,
                $"{ready} is ready to go up",
                s.ReadyNextWeightKg is decimal next
                    ? $"You've held steady long enough. Try {Trim(next)}kg."
                    : "You've held steady long enough. Add a little."));
        }

        // A comeback is the intersection of two facts already computed: something dropped, and the
        // area it belongs to being the least trained. Resuming it is the single change that moves
        // both — which is why it outranks reporting either on its own.
        // Never in a group that is being told to rest. Both facts can be true at once — one cardio
        // movement hammered this week while another sat idle for six weeks — and the engine would
        // then tell the member to rest a group and train it in the same breath. Rest wins: it is the
        // one that protects them, and the comeback will still be there next week.
        var comebackAllowed = !string.Equals(s.WeakestMuscleGroup, s.FatiguedMuscleGroup, StringComparison.OrdinalIgnoreCase);

        var comeback = comebackAllowed && s.WeakestMuscleGroup is { Length: > 0 } weakest
            ? s.GoneQuiet.FirstOrDefault(q =>
                string.Equals(q.MuscleGroup, weakest, StringComparison.OrdinalIgnoreCase))
            : default;

        if (comeback.ExerciseName is { Length: > 0 })
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.Comeback,
                $"Pick {comeback.ExerciseName} back up",
                $"{Weeks(comeback.DaysSince)} since your last one, and {s.WeakestMuscleGroup} is your least-trained area."));
        }

        if (s.PlateauExerciseName is { Length: > 0 } plateau)
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.Plateau,
                $"{plateau} hasn't moved",
                "Same weight and reps for a while now — worth changing something."));
        }

        // Whatever was dropped longest ago, unless it is already the comeback above, and never from
        // the group being rested — the same contradiction, wearing a different heading.
        var quiet = s.GoneQuiet.FirstOrDefault(q =>
            !string.Equals(q.ExerciseName, comeback.ExerciseName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(q.MuscleGroup, s.FatiguedMuscleGroup, StringComparison.OrdinalIgnoreCase));
        if (quiet.ExerciseName is { Length: > 0 })
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.GoneQuiet,
                $"{quiet.ExerciseName} has gone quiet",
                $"You haven't trained it in {Weeks(quiet.DaysSince)}."));
        }

        if (s.MomentumExerciseName is { Length: > 0 } momentum && s.MomentumGainKg > 0 && s.MomentumWeeks > 0)
        {
            insights.Add(new TrainingInsight(
                TrainingInsightKind.Momentum,
                $"{momentum} is climbing",
                $"Up {Trim(s.MomentumGainKg)}kg over {s.MomentumWeeks} week{(s.MomentumWeeks == 1 ? "" : "s")}."));
        }

        return insights.Take(MaxInsights).ToList();
    }

    private static string Weeks(int days) => days switch
    {
        < 14 => $"{days} days",
        < 60 => $"{(int)Math.Round(days / 7.0)} weeks",
        _ => $"{(int)Math.Round(days / 30.0)} months"
    };

    private static string Trim(decimal value) => value.ToString("0.##");
}

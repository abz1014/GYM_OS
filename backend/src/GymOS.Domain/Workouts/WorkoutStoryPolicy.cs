namespace GymOS.Domain.Workouts;

/// <summary>A best set during a session, and what it beat.</summary>
/// <param name="PreviousValue">The member's previous best on this exercise and measure, when there
/// was one. Absent means this is the first — a first is worth saying differently from a gain.</param>
/// <param name="Prominence">How much this measure means to a member, lower being more. One lift can
/// set a heaviest weight, an estimated one-rep max and a session volume in the same set, and their
/// numbers are not comparable — a volume figure dwarfs a load figure while saying far less. Ranking
/// by it rather than by the raw number keeps the sentence on the number the member trains by.</param>
public readonly record struct RecordGain(
    string ExerciseName, decimal Value, string Unit, decimal? PreviousValue, int Prominence = 0);

/// <summary>What happened in one session, in the words a member would use.</summary>
public readonly record struct SessionStory(string Title, IReadOnlyList<string> Lines)
{
    /// <summary>The story as one line, for surfaces that have room for a sentence and not a list.</summary>
    public string OneLine => string.Join(" ", Lines);
}

/// <summary>
/// Turns a finished session into a few plain sentences.
///
/// A member should not have to read a chart to find out how their training went. The app holds
/// every fact needed to say it outright — what was trained, what got heavier, by how much — and it
/// was spending those facts on strings like "Bench Press 3×8" and, when a session had nothing
/// attached, "(nothing recorded)". That last one is the worst of them: a member reads "nothing
/// recorded" as "I lost my workout", when the truth is only that the details were never captured.
///
/// Deterministic on purpose. Everything here is a fact the engine already computed, phrased; there
/// is no model, no inference, and nothing that can say something untrue about a member's own
/// training. One policy, so the timeline, a notification and a weekly summary all tell the same
/// story rather than three teams writing three versions of it.
/// </summary>
public static class WorkoutStoryPolicy
{
    public const string NoDetailTitle = "Workout completed";
    public const string NoDetailLine = "No exercise details recorded.";

    /// <summary>How many movements are named before the rest are counted.</summary>
    public const int MaxNamedMovements = 3;

    /// <summary>
    /// One logged movement in words, using only the measurements it actually has.
    ///
    /// The timeline built this inline as "{Name} {Sets}×{Reps}", which was fine while every movement
    /// had reps. Since RepsCompleted became nullable a run rendered as "Treadmill Run 1×" — a
    /// dangling multiplication sign that reads as a number the app lost, and says nothing about the
    /// three kilometres the member actually ran.
    ///
    /// The rule matches the member app's own formatter (frontend lib/measurement.ts): a measurement
    /// appears when it exists and is silent when it does not, with no placeholder standing in for an
    /// absent one.
    /// </summary>
    public static string DescribeMovement(
        string exerciseName, int sets, int? reps, int? durationSeconds, decimal? distanceMeters)
    {
        var measures = new List<string>();
        if (reps is { } r) measures.Add(r.ToString());
        if (distanceMeters is { } metres)
        {
            measures.Add(metres >= 1000 ? $"{Trim(metres / 1000)}km" : $"{Trim(metres)}m");
        }
        if (durationSeconds is { } seconds)
        {
            measures.Add(seconds < 60 ? $"{seconds}s" : $"{seconds / 60}:{seconds % 60:00}");
        }

        // "in" reads correctly for a distance covered over a time; a bare list gives "3km 20:00".
        return measures.Count > 0
            ? $"{exerciseName} {sets}×{string.Join(" in ", measures)}"
            // A set count on its own is a real record — the movement was done, the detail was not
            // captured — and saying so beats printing an operator with no operand.
            : $"{exerciseName} {sets} {(sets == 1 ? "set" : "sets")}";
    }

    /// <summary>
    /// Tells the story of a session.
    /// </summary>
    /// <param name="character">What the session was, from SessionCharacterPolicy.</param>
    /// <param name="movements">One per exercise, already formatted — see DescribeMovement.</param>
    /// <param name="records">Bests set during this session.</param>
    public static SessionStory Tell(
        string character,
        IReadOnlyList<string> movements,
        IReadOnlyList<RecordGain> records)
    {
        // A check-in with nothing logged against it is a real thing that happens, and the member did
        // train — say what is true without implying their work went missing.
        if (movements.Count == 0)
        {
            return new SessionStory(NoDetailTitle, [NoDetailLine]);
        }

        var lines = new List<string> { DescribeMovements(movements) };

        // Records are named, not counted: "3 personal records" is a statistic, "your best bench yet"
        // is the thing the member came for. Best gain first so the strongest sentence leads.
        foreach (var record in records
                     .GroupBy(r => r.ExerciseName)
                     .Select(BestOf)
                     .OrderByDescending(r => r.PreviousValue is null ? 0m : r.Value - r.PreviousValue.Value)
                     .ThenBy(r => r.ExerciseName, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(DescribeRecord(record));
        }

        return new SessionStory(character, lines);
    }

    /// <summary>
    /// The single record worth reporting for one exercise. One lift can set several at once and they
    /// all describe the same set, so the most meaningful measure wins outright — never the largest
    /// number, which would always be the volume and would tell the member the least.
    /// </summary>
    private static RecordGain BestOf(IGrouping<string, RecordGain> forOneExercise) =>
        forOneExercise
            .OrderBy(r => r.Prominence)
            .ThenByDescending(r => r.PreviousValue is null ? 0m : r.Value - r.PreviousValue.Value)
            .First();

    private static string DescribeMovements(IReadOnlyList<string> movements)
    {
        if (movements.Count <= MaxNamedMovements) return $"{string.Join(", ", movements)}.";

        var rest = movements.Count - MaxNamedMovements;
        return $"{string.Join(", ", movements.Take(MaxNamedMovements))}, and {rest} more.";
    }

    private static string DescribeRecord(RecordGain record)
    {
        var value = Trim(record.Value);

        // A first best is a milestone, not an improvement — calling it a gain would be a lie about a
        // number the member can check.
        if (record.PreviousValue is not decimal previous || previous >= record.Value)
        {
            return $"Your best {record.ExerciseName} yet at {value}{record.Unit}.";
        }

        return $"{record.ExerciseName} up {Trim(record.Value - previous)}{record.Unit} to {value}{record.Unit} — a new best.";
    }

    private static string Trim(decimal value) => value.ToString("0.##");
}

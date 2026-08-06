namespace GymOS.Domain.Workouts;

/// <summary>
/// What a session was, worked out from what the member actually lifted.
///
/// Every self-logged session used to read "Custom workout" — the member's own history, described
/// back to them in the least useful words available. A session is a thing people remember ("legs",
/// "back and biceps"), and naming it is the difference between a list of rows and a training story.
///
/// This asks the member nothing. Asking "was this push or pull?" would buy one word for a question
/// on every visit, and the answer is already sitting in the exercises they picked.
///
/// Nor does it guess. Muscle groups here are free text owned by the gym, not an enum we control:
/// they differ per site, may not be English, and a coarse one like "Arms" spans both directions at
/// once — a curl pulls, a pushdown pushes. So the session is named with the gym's own labels, and
/// only promoted to "Push day"/"Pull day"/"Leg day" when several distinct groups all agree. A single
/// group is always named outright, because "Back" says more than "Pull day" and cannot be wrong.
/// Anything unrecognised simply keeps its own name rather than being forced into a category.
/// </summary>
public static class SessionCharacterPolicy
{
    /// <summary>Beyond this many muscle groups a session stops being about any of them.</summary>
    public const int MaxNamedGroups = 3;

    public const string Unknown = "Workout";
    public const string FullBody = "Full body";

    private static readonly HashSet<string> PushGroups = new(StringComparer.OrdinalIgnoreCase)
        { "chest", "pecs", "shoulders", "shoulder", "delts", "triceps", "tricep" };

    private static readonly HashSet<string> PullGroups = new(StringComparer.OrdinalIgnoreCase)
        { "back", "lats", "traps", "biceps", "bicep" };

    private static readonly HashSet<string> LegGroups = new(StringComparer.OrdinalIgnoreCase)
        { "legs", "leg", "quads", "quadriceps", "hamstrings", "glutes", "calves" };

    /// <summary>
    /// Names a session from the muscle group of each exercise in it. Pass one entry per exercise —
    /// repeats are what decide which group led the session, so four back movements and one curl
    /// reads "Back &amp; Arms" rather than the other way round.
    /// </summary>
    public static string Describe(IEnumerable<string?> muscleGroupPerExercise)
    {
        var groups = muscleGroupPerExercise
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g!.Trim())
            .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
            // The group trained most leads the name; ties fall back to alphabetical so the same
            // session never gets two different names on two different reads.
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (groups.Count == 0) return Unknown;

        // Only worth a pattern name once it unifies more than one group — otherwise the group's own
        // name is both more specific and impossible to get wrong.
        if (groups.Count > 1)
        {
            if (groups.All(PushGroups.Contains)) return "Push day";
            if (groups.All(PullGroups.Contains)) return "Pull day";
            if (groups.All(LegGroups.Contains)) return "Leg day";
        }

        return groups.Count > MaxNamedGroups ? FullBody : Join(groups);
    }

    private static string Join(IReadOnlyList<string> groups) => groups.Count switch
    {
        1 => groups[0],
        2 => $"{groups[0]} & {groups[1]}",
        _ => $"{string.Join(", ", groups.Take(groups.Count - 1))} & {groups[^1]}"
    };
}

namespace GymOS.Domain.Members;

/// <summary>
/// One movement in the gym, and what the member has done on it.
/// </summary>
/// <param name="Sessions">Zero means never tried — the entry is still listed, because what a member
/// has not done yet is the whole point of a passport.</param>
/// <param name="BestWeightKg">Their heaviest on it, or null for a movement carrying no load. A
/// bodyweight plank has no best weight, and reporting "0kg" would read as a failure rather than as
/// the absence of a number.</param>
public readonly record struct PassportEntry(
    Guid ExerciseId,
    string ExerciseName,
    string? MuscleGroup,
    int Sessions,
    decimal? BestWeightKg,
    DateOnly? LastTrained,
    int? DaysSinceLastTrained)
{
    public bool Tried => Sessions > 0;

    /// <summary>Tried once, then left alone long enough that it has dropped out of their training.</summary>
    public bool GoneQuiet => DaysSinceLastTrained >= GymPassportPolicy.QuietAfterDays;
}

/// <summary>One kind of equipment, and how much of it the member has covered.</summary>
public readonly record struct PassportStamp(string Equipment, int Tried, int Available, IReadOnlyList<PassportEntry> Entries)
{
    public bool Complete => Available > 0 && Tried == Available;
}

/// <summary>The member's map of their own gym.</summary>
/// <param name="GoneQuiet">The few movements the member has drifted furthest from. Bounded rather
/// than exhaustive: in a gym with a hundred movements, listing everything untouched for a month
/// would flag ninety of them and mean nothing.</param>
public readonly record struct GymPassport(
    int Tried, int Available, int PercentCovered,
    IReadOnlyList<PassportStamp> Stamps, IReadOnlyList<PassportEntry> GoneQuiet)
{
    public bool Complete => Available > 0 && Tried == Available;
}

/// <summary>
/// What of the gym a member has actually used, and what is still waiting.
///
/// Everything else in the product reports what a member did. None of it can answer "what haven't I
/// touched" — mastery is built from the sessions that exist, so a machine nobody has been near is
/// invisible to it. That absence is the interesting part: it is the difference between a record of
/// training and a map of the place you train in, and it is the only surface here that can send
/// somebody to a corner of the gym they have walked past for a year.
///
/// Built from the gym's own catalogue, so a site that has never heard of a leg press simply does not
/// have one to cover. Nothing is scored and nothing is judged: an untried movement is reported as a
/// fact, not a gap, because a member who is told they are 40% of a gym has been given a mark out of
/// ten for turning up.
/// </summary>
public static class GymPassportPolicy
{
    /// <summary>Equipment shown for a movement whose kit the gym never labelled.</summary>
    public const string Unlabelled = "Other";

    /// <summary>
    /// How long a movement has to go untouched before it counts as dropped rather than merely not
    /// due. Someone training three times a week across a normal catalogue comes back to any given
    /// movement every couple of weeks, so a month is comfortably past "it just was not this week"
    /// without nagging anyone following a programme that rotates.
    /// </summary>
    public const int QuietAfterDays = 30;

    /// <summary>How many of the neglected are worth naming. The rest is a list nobody reads.</summary>
    public const int MaxQuietHighlighted = 3;

    /// <summary>
    /// Builds the passport.
    /// </summary>
    /// <param name="catalogue">Every exercise the gym offers. The denominator is the gym's own, so
    /// coverage means something specific to that site rather than to some notional complete gym.</param>
    /// <param name="today">The gym's today, so "days since" means the same as every other day on
    /// screen — see GymDay.</param>
    public static GymPassport Build(IEnumerable<PassportCatalogueEntry> catalogue, DateOnly today)
    {
        var entries = catalogue.ToList();

        var stamps = entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Equipment) ? Unlabelled : e.Equipment!.Trim())
            .Select(g => new PassportStamp(
                g.Key,
                g.Count(e => e.Sessions > 0),
                g.Count(),
                g
                    // Tried first, most-used before least — it is the member's record, and the record
                    // reads better than the to-do list. What is left follows, alphabetically.
                    .OrderByDescending(e => e.Sessions > 0)
                    .ThenByDescending(e => e.Sessions)
                    .ThenBy(e => e.ExerciseName, StringComparer.OrdinalIgnoreCase)
                    .Select(e => Entry(e, today))
                    .ToList()))
            .OrderByDescending(s => s.Tried)
            .ThenBy(s => s.Equipment, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tried = entries.Count(e => e.Sessions > 0);
        var available = entries.Count;

        // Named worst-first and capped, so this stays a short prompt rather than a backlog.
        var goneQuiet = stamps
            .SelectMany(s => s.Entries)
            .Where(e => e.GoneQuiet)
            .OrderByDescending(e => e.DaysSinceLastTrained)
            .ThenBy(e => e.ExerciseName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxQuietHighlighted)
            .ToList();

        return new GymPassport(tried, available, PercentCovered(tried, available), stamps, goneQuiet);
    }

    /// <summary>Rounded away from zero so a member who has tried something is never shown 0%.</summary>
    public static int PercentCovered(int tried, int available)
        => available <= 0 ? 0 : (int)Math.Round(tried * 100.0 / available, MidpointRounding.AwayFromZero);

    private static PassportEntry Entry(PassportCatalogueEntry e, DateOnly today) => new(
        e.ExerciseId,
        e.ExerciseName,
        e.MuscleGroup,
        e.Sessions,
        // Zero is what the store holds for a movement carrying no load; it is not a best.
        e.BestWeightKg > 0 ? e.BestWeightKg : null,
        e.LastTrained,
        // Clamped at zero: a record dated in the future is bad data, not training yet to happen.
        e.LastTrained is DateOnly d ? Math.Max(0, today.DayNumber - d.DayNumber) : null);
}

/// <summary>One exercise the gym offers, joined to whatever the member has done on it.</summary>
public readonly record struct PassportCatalogueEntry(
    Guid ExerciseId,
    string ExerciseName,
    string? MuscleGroup,
    string? Equipment,
    int Sessions,
    decimal BestWeightKg,
    DateOnly? LastTrained);

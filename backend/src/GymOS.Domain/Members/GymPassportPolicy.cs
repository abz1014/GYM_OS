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
    DateOnly? LastTrained)
{
    public bool Tried => Sessions > 0;
}

/// <summary>One kind of equipment, and how much of it the member has covered.</summary>
public readonly record struct PassportStamp(string Equipment, int Tried, int Available, IReadOnlyList<PassportEntry> Entries)
{
    public bool Complete => Available > 0 && Tried == Available;
}

/// <summary>The member's map of their own gym.</summary>
public readonly record struct GymPassport(int Tried, int Available, int PercentCovered, IReadOnlyList<PassportStamp> Stamps)
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
    /// Builds the passport.
    /// </summary>
    /// <param name="catalogue">Every exercise the gym offers. The denominator is the gym's own, so
    /// coverage means something specific to that site rather than to some notional complete gym.</param>
    public static GymPassport Build(IEnumerable<PassportCatalogueEntry> catalogue)
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
                    .Select(Entry)
                    .ToList()))
            .OrderByDescending(s => s.Tried)
            .ThenBy(s => s.Equipment, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tried = entries.Count(e => e.Sessions > 0);
        var available = entries.Count;

        return new GymPassport(tried, available, PercentCovered(tried, available), stamps);
    }

    /// <summary>Rounded away from zero so a member who has tried something is never shown 0%.</summary>
    public static int PercentCovered(int tried, int available)
        => available <= 0 ? 0 : (int)Math.Round(tried * 100.0 / available, MidpointRounding.AwayFromZero);

    private static PassportEntry Entry(PassportCatalogueEntry e) => new(
        e.ExerciseId,
        e.ExerciseName,
        e.MuscleGroup,
        e.Sessions,
        // Zero is what the store holds for a movement carrying no load; it is not a best.
        e.BestWeightKg > 0 ? e.BestWeightKg : null,
        e.LastTrained);
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

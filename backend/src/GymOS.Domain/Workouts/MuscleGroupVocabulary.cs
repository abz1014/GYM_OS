namespace GymOS.Domain.Workouts;

/// <summary>
/// Which side of the body a group is drawn on. The body map needs this and nothing else knows it.
/// </summary>
public enum BodyView
{
    Front,
    Back,
    Both
}

/// <summary>
/// One canonical muscle group: a stable key, a name for people, and where it sits on a body.
/// </summary>
/// <param name="Key">Stable, lower-case, safe to put in a URL or a CSS class. Never shown.</param>
/// <param name="DisplayName">What the member reads.</param>
/// <param name="Order">Catalogue order — big compound groups first, cardio and conditioning last.</param>
/// <param name="View">Which body view this group is drawn on.</param>
public record MuscleGroupInfo(string Key, string DisplayName, int Order, BodyView View);

/// <summary>
/// The one place free-text muscle labels become a fixed set of groups.
///
/// <see cref="Exercise.MuscleGroup"/> is a string a gym owner types. That is deliberate — nobody
/// should have to file a change request to add "Adductors" — but it means two screens that want to
/// GROUP by muscle will disagree the moment someone types "Quads" where the seeder wrote "Legs". The
/// exercise picker's categories and the body map's regions are exactly those two screens, and a
/// category the map cannot shade (or a region the picker cannot fill) is a hole the member sees.
///
/// So: free text stays the input, and this is the only function allowed to interpret it. Resolution
/// is case-insensitive, matches on well-known synonyms, and falls back to <see cref="Other"/> rather
/// than guessing — an unrecognised label lands in a real, visible group called "Other" instead of
/// silently vanishing from a picker that claims to list everything.
///
/// It lives in Domain rather than beside either screen because both screens must reach the same
/// answer; two copies of this table would drift the first time one of them gained a synonym.
/// </summary>
public static class MuscleGroupVocabulary
{
    public static readonly MuscleGroupInfo Chest = new("chest", "Chest", 1, BodyView.Front);
    public static readonly MuscleGroupInfo Back = new("back", "Back", 2, BodyView.Back);
    public static readonly MuscleGroupInfo Shoulders = new("shoulders", "Shoulders", 3, BodyView.Both);
    public static readonly MuscleGroupInfo Arms = new("arms", "Arms", 4, BodyView.Both);
    public static readonly MuscleGroupInfo Legs = new("legs", "Legs", 5, BodyView.Both);
    public static readonly MuscleGroupInfo Core = new("core", "Core", 6, BodyView.Front);
    public static readonly MuscleGroupInfo FullBody = new("fullbody", "Full body", 7, BodyView.Both);
    public static readonly MuscleGroupInfo Cardio = new("cardio", "Cardio", 8, BodyView.Both);

    /// <summary>
    /// Where anything unrecognised goes. Deliberately a real group with a real name: a gym that types
    /// "Adductors" should still find that exercise in the picker, filed somewhere honest, rather than
    /// having it disappear because this table had never heard of it.
    /// </summary>
    public static readonly MuscleGroupInfo Other = new("other", "Other", 9, BodyView.Both);

    /// <summary>Every group, in catalogue order, including Other.</summary>
    public static IReadOnlyList<MuscleGroupInfo> All { get; } =
        [Chest, Back, Shoulders, Arms, Legs, Core, FullBody, Cardio, Other];

    /// <summary>
    /// Synonyms a gym plausibly types, mapped onto the canonical set.
    ///
    /// Individual muscles resolve to the group they belong to — "Lats" and "Traps" are Back, "Quads"
    /// and "Glutes" are Legs — because the member is choosing what to train today, and nobody plans a
    /// session by trapezius. Anything finer than this belongs in the exercise name.
    /// </summary>
    private static readonly Dictionary<string, MuscleGroupInfo> ByLabel =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = Chest, ["pecs"] = Chest, ["pectorals"] = Chest,

            ["back"] = Back, ["lats"] = Back, ["latissimus"] = Back, ["traps"] = Back,
            ["trapezius"] = Back, ["rhomboids"] = Back, ["upper back"] = Back, ["lower back"] = Back,

            ["shoulders"] = Shoulders, ["shoulder"] = Shoulders, ["delts"] = Shoulders,
            ["deltoids"] = Shoulders,

            ["arms"] = Arms, ["arm"] = Arms, ["biceps"] = Arms, ["triceps"] = Arms,
            ["forearms"] = Arms,

            ["legs"] = Legs, ["leg"] = Legs, ["quads"] = Legs, ["quadriceps"] = Legs,
            ["hamstrings"] = Legs, ["glutes"] = Legs, ["calves"] = Legs, ["lower body"] = Legs,

            ["core"] = Core, ["abs"] = Core, ["abdominals"] = Core, ["obliques"] = Core,

            ["full body"] = FullBody, ["fullbody"] = FullBody, ["total body"] = FullBody,
            ["compound"] = FullBody,

            ["cardio"] = Cardio, ["conditioning"] = Cardio, ["endurance"] = Cardio,
        };

    /// <summary>
    /// The group a free-text label belongs to. Null, blank and unknown all resolve to
    /// <see cref="Other"/> — never to a guess, and never to nothing.
    /// </summary>
    public static MuscleGroupInfo Resolve(string? muscleGroup)
    {
        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            return Other;
        }

        return ByLabel.TryGetValue(muscleGroup.Trim(), out var info) ? info : Other;
    }
}

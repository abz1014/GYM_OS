using GymOS.Application.Common.Coaching;
namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>
/// What actually happened because a member logged a workout.
///
/// Logging is the one moment a member gives the app something, and until now it answered with a
/// toast that claimed a flat "+50 XP" whether or not that was the real number. Everything the
/// Member Experience Engine computes off a logged session — XP, a level, personal records,
/// achievements, the weekly ring, the streak, challenge progress — was already being written; it
/// just wasn't being told back to the person who earned it.
///
/// Assembled by diffing the member's state either side of the log, so the numbers are what the
/// engine really did rather than what the client guessed.
/// </summary>
/// <param name="XpEarned">Total XP from this session, including any personal-record and achievement
/// bonuses — the change in the member's total, not just the base workout award.</param>
/// <param name="GoalJustMet">True only when THIS session closed the weekly ring. Distinct from
/// <paramref name="GoalMet"/>, which is also true for every extra session after it.</param>
/// <param name="Character">What the session was — "Push day", "Legs", "Back &amp; Arms" — derived from
/// the muscle groups trained rather than asked for. See SessionCharacterPolicy.</param>
public record MyWorkoutResultDto(
    Guid WorkoutLogId,
    string Character,
    int XpEarned,
    int Level,
    bool LeveledUp,
    int SessionsThisWeek,
    int WeeklySessionGoal,
    bool GoalMet,
    bool GoalJustMet,
    int WorkoutStreakWeeks,
    IReadOnlyList<MyNewRecordDto> NewRecords,
    IReadOnlyList<MyNewAchievementDto> NewAchievements,
    IReadOnlyList<MyChallengeStepDto> ChallengeProgress);

/// <summary>A personal record set by this session. Tied to the workout log itself, not inferred.</summary>
public record MyNewRecordDto(string ExerciseName, string Type, decimal Value);

public record MyNewAchievementDto(string Code, string Name, string Description, string Tier, string Icon);

/// <summary>Progress on a challenge the member has joined, and whether this session finished it.</summary>
public record MyChallengeStepDto(string Name, int WorkoutsLogged, int TargetWorkoutCount, bool JustCompleted);

/// <summary>One pre-filled movement in a proposed session.</summary>
public record MyProposedEntryDto(Guid ExerciseId, string ExerciseName, int Sets, int Reps, decimal? WeightKg);

/// <summary>
/// The session the app believes the member is about to do, ready to confirm in one tap.
/// See GetMyNextSessionQuery and SessionProposalPolicy for how the tier is chosen.
/// </summary>
/// <param name="Source">TrainerPlan, RepeatLast, Starter or None — what the proposal is based on,
/// so the screen can say where it came from instead of presenting numbers with no provenance.</param>
public record MySessionProposalDto(string Source, bool CanConfirm, IReadOnlyList<MyProposedEntryDto> Entries);

/// <summary>
/// One movement in the picker, with everything needed to choose it and nothing that needs typing.
/// </summary>
/// <param name="MuscleGroupKey">The canonical group — see MuscleGroupVocabulary. Stable, for code.</param>
/// <param name="MuscleGroupName">The same group, for people.</param>
/// <param name="LoadType">Weighted, Bodyweight, Timed or Distance. The screen asks for a weight only
/// when there is one to ask for.</param>
/// <param name="LastPerformedAt">Null until the member has done this movement. Drives "Recent".</param>
/// <param name="TimesPerformed">Distinct sessions, not sets — how familiar this movement is.</param>
/// <param name="LastSets">Sets in the member's most recent session of this movement, summed across
/// the per-set rows the logger writes. Null with no history.</param>
/// <param name="LastReps">Reps from the heaviest set of that session.</param>
/// <param name="LastWeightKg">Load from the heaviest set of that session. Always null for anything
/// that is not Weighted — a plank has no kilograms, and a placeholder zero would be a claim.</param>
public record MyCatalogueExerciseDto(
    Guid ExerciseId,
    string Name,
    string MuscleGroupKey,
    string MuscleGroupName,
    string? Equipment,
    string LoadType,
    DateTimeOffset? LastPerformedAt,
    int TimesPerformed,
    int? LastSets,
    int? LastReps,
    decimal? LastWeightKg);

/// <summary>One muscle group's worth of the catalogue, already sorted by name.</summary>
public record MyExerciseGroupDto(string Key, string Name, IReadOnlyList<MyCatalogueExerciseDto> Exercises);

/// <summary>
/// The whole picker in one call — see GetMyExerciseCatalogueQuery. Groups arrive in catalogue order
/// (the big compound groups first, cardio last) so the client never has to know that ordering.
/// </summary>
public record MyExerciseCatalogueDto(IReadOnlyList<MyExerciseGroupDto> Groups);

/// <summary>
/// The member's coach and the conversation with them.
/// </summary>
/// <param name="CanSend">False once a pairing ends — the history stays readable, but there is
/// nobody on the other end to add to it.</param>
/// <param name="HasOlder">More exists above the window. A year of coaching is thousands of messages
/// and nobody opens a conversation to read the first one; sending them all cost a megabyte a view.</param>
public record MyCoachDto(
    Guid? TrainerId,
    string? TrainerName,
    bool CanSend,
    int UnreadCount,
    bool HasOlder,
    IReadOnlyList<MyCoachMessageDto> Messages);

/// <param name="Author">"Member" or "Trainer".</param>
/// <param name="WorkoutLogId">The session this message is about, when it is about one.</param>
/// <param name="Session">The workout this message is about, when it is about one and that workout
/// still exists. Null is the ordinary case — "see you Thursday" is about nothing.</param>
public record MyCoachMessageDto(
    Guid Id, string Author, string Body, DateTimeOffset SentAt, bool Read, Guid? WorkoutLogId,
    CoachMessageSessionDto? Session);

/// <summary>
/// The member's map of their own gym — what they have used and what is still waiting.
/// </summary>
/// <param name="Available">Size of this gym's catalogue. Coverage is measured against the site the
/// member actually trains at, not a notional complete gym.</param>
/// <param name="GoneQuiet">The few movements they have drifted furthest from — bounded, because a
/// large catalogue would otherwise flag most of itself and mean nothing.</param>
public record MyPassportDto(
    int Tried, int Available, int PercentCovered, bool Complete,
    IReadOnlyList<MyPassportStampDto> Stamps, IReadOnlyList<MyPassportEntryDto> GoneQuiet);

/// <summary>One kind of equipment and how much of it the member has covered.</summary>
public record MyPassportStampDto(
    string Equipment, int Tried, int Available, bool Complete, IReadOnlyList<MyPassportEntryDto> Entries);

/// <param name="BestWeightKg">Null for a movement carrying no load — a plank has no best weight, and
/// "0kg" would read as a failure rather than the absence of a number.</param>
public record MyPassportEntryDto(
    Guid ExerciseId, string ExerciseName, string? MuscleGroup, bool Tried, int Sessions,
    decimal? BestWeightKg, DateOnly? LastTrained, int? DaysSinceLastTrained, bool GoneQuiet);

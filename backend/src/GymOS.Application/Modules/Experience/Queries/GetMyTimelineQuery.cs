using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's story: sessions, measurements, photos, achieved goals and achievements merged into
/// one chronological feed, newest first. Self-scoped via MyMemberResolver.
///
/// The sessions are the spine of it, and they were the one thing missing — a member's history showed
/// every record they had ever set but never the training that produced them. Worse, it showed them
/// flat: a personal record is written per exercise AND per record type, so a single good session
/// emits half a dozen entries. On real data that made the feed 93% "New PR" lines — 201 of 217 for
/// the demo member — which is not a story, it is a wall.
///
/// So a record now belongs to the session that set it rather than sitting beside it, which is what
/// PersonalRecord.WorkoutLogId was always for. A record with no session attached is still shown on
/// its own: every one in the current data is linked, but an imported or backfilled record need not
/// be, and silently dropping a member's achievement would be worse than an untidy feed.
///
/// Still a pure read composition over append-only tables — no new source of truth.
/// </summary>
public record GetMyTimelineQuery : IQuery<List<MyTimelineEntryDto>>;

public class GetMyTimelineQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyTimelineQuery, List<MyTimelineEntryDto>>
{
    public async Task<List<MyTimelineEntryDto>> Handle(GetMyTimelineQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var entries = new List<MyTimelineEntryDto>();

        var measurements = await db.MemberMeasurements.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .ToListAsync(cancellationToken);
        entries.AddRange(measurements.Select(m => new MyTimelineEntryDto(
            "Measurement",
            new DateTimeOffset(m.MeasuredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            "Body measurement logged",
            DescribeMeasurement(m),
            null)));

        var photos = await db.ProgressPhotos.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .ToListAsync(cancellationToken);
        entries.AddRange(photos.Select(p => new MyTimelineEntryDto(
            "Photo", p.TakenAt, "Progress photo added", p.Notes, p.PhotoUrl)));

        var achievedGoals = await db.MemberGoals.AsNoTracking()
            .Where(g => g.MemberId == memberId && g.IsAchieved && g.AchievedAt != null)
            .ToListAsync(cancellationToken);
        entries.AddRange(achievedGoals.Select(g => new MyTimelineEntryDto(
            "GoalAchieved", g.AchievedAt!.Value, $"Goal achieved: {g.Title}", null, null)));

        var records = await db.PersonalRecords.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Select(r => new { r.ExerciseId, r.Type, r.Value, r.AchievedAt, r.WorkoutLogId })
            .ToListAsync(cancellationToken);

        var sessions = await db.WorkoutLogs.AsNoTracking()
            .Where(w => w.MemberId == memberId)
            .Select(w => new
            {
                w.Id,
                w.LoggedAt,
                Entries = w.Entries.Select(e => new { e.ExerciseId, e.SetsCompleted, e.RepsCompleted, e.WeightKg }).ToList()
            })
            .ToListAsync(cancellationToken);

        // One catalogue lookup covers both the records and the sessions. Muscle group comes along
        // because it is what names a session (SessionCharacterPolicy) — the same name the member sees
        // everywhere else a session is listed.
        var exerciseIds = records.Select(r => r.ExerciseId)
            .Concat(sessions.SelectMany(s => s.Entries.Select(e => e.ExerciseId)))
            .Distinct()
            .ToList();
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => new { e.Name, e.MuscleGroup }, cancellationToken);

        string NameOf(Guid id) => exercises.GetValueOrDefault(id)?.Name ?? "Unknown";

        var recordsBySession = records
            .Where(r => r.WorkoutLogId != null)
            .GroupBy(r => r.WorkoutLogId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // What each record beat. Every one of the member's records is already loaded, so the previous
        // best on the same exercise and measure is a lookup rather than another round trip — and it
        // is the difference between "a new best" and "up 2.5kg to 62.5kg", which is the sentence
        // worth reading.
        var recordHistory = records
            .GroupBy(r => (r.ExerciseId, r.Type))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.AchievedAt).ToList());

        // Matched on when it was set, not on the value: a member who equals a record has two rows
        // with the same number, and looking the value up would find the wrong one.
        decimal? PreviousBest(Guid exerciseId, PersonalRecordType type, DateTimeOffset achievedAt) =>
            recordHistory[(exerciseId, type)]
                .Where(r => r.AchievedAt < achievedAt)
                .Select(r => (decimal?)r.Value)
                .LastOrDefault();

        entries.AddRange(sessions.Select(s =>
        {
            var character = SessionCharacterPolicy.Describe(
                s.Entries.Select(e => exercises.GetValueOrDefault(e.ExerciseId)?.MuscleGroup));

            var movements = s.Entries
                .Select(e => $"{NameOf(e.ExerciseId)} {e.SetsCompleted}×{e.RepsCompleted}")
                .ToList();

            var gains = recordsBySession.TryGetValue(s.Id, out var setHere)
                ? setHere.Select(r => new RecordGain(
                    NameOf(r.ExerciseId),
                    r.Value,
                    UnitFor(r.Type),
                    PreviousBest(r.ExerciseId, r.Type, r.AchievedAt),
                    ProminenceOf(r.Type))).ToList()
                : [];

            var story = WorkoutStoryPolicy.Tell(character, movements, gains);
            return new MyTimelineEntryDto("Workout", s.LoggedAt, story.Title, story.OneLine, null);
        }));

        // Anything not attached to a session still stands on its own, and says why it looks different
        // from everything around it. Nothing in the current data lands here — but a record carried in
        // from whatever the gym used before has no session to belong to, and dropping it silently
        // would take an achievement off a member who earned it somewhere this app never saw.
        entries.AddRange(records
            .Where(r => r.WorkoutLogId == null)
            .Select(r => new MyTimelineEntryDto(
                "PersonalRecord",
                r.AchievedAt,
                $"{NameOf(r.ExerciseId)} — {FormatPrType(r.Type).ToLowerInvariant()} {r.Value:0.##}{UnitFor(r.Type)}",
                "Imported from a previous system, so there is no session attached.",
                null)));

        var unlockedAchievements = await db.MemberAchievements.AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .ToListAsync(cancellationToken);
        var catalogByCode = AchievementCatalog.All.ToDictionary(a => a.Code);
        entries.AddRange(unlockedAchievements
            .Where(a => catalogByCode.ContainsKey(a.Code))
            .Select(a =>
            {
                var definition = catalogByCode[a.Code];
                return new MyTimelineEntryDto("Achievement", a.UnlockedAt, $"Achievement unlocked: {definition.Name}", definition.Description, null);
            }));

        return entries.OrderByDescending(e => e.OccurredAt).ToList();
    }

    private static string? DescribeMeasurement(MemberMeasurement m)
    {
        var parts = new List<string>();
        if (m.WeightKg is { } weight) parts.Add($"Weight {weight}kg");
        if (m.BodyFatPercentage is { } bodyFat) parts.Add($"Body fat {bodyFat}%");
        if (m.ChestCm is { } chest) parts.Add($"Chest {chest}cm");
        if (m.WaistCm is { } waist) parts.Add($"Waist {waist}cm");
        if (m.HipCm is { } hip) parts.Add($"Hip {hip}cm");
        if (m.ArmCm is { } arm) parts.Add($"Arm {arm}cm");
        if (m.ThighCm is { } thigh) parts.Add($"Thigh {thigh}cm");
        return parts.Count > 0 ? string.Join(", ", parts) : m.Notes;
    }

    /// <summary>
    /// Which measure to tell the story with when one lift set several. The weight on the bar is what
    /// a member trains by and remembers; an estimate derived from it comes next; total volume last,
    /// because its number is the largest and means the least.
    /// </summary>
    private static int ProminenceOf(PersonalRecordType type) => type switch
    {
        PersonalRecordType.MaxWeight => 0,
        PersonalRecordType.EstimatedOneRepMax => 1,
        PersonalRecordType.SessionVolume => 2,
        _ => 3
    };

    /// <summary>What a record's number is measured in, so a story can say "up 2.5kg" and mean it.</summary>
    private static string UnitFor(PersonalRecordType type) => type switch
    {
        PersonalRecordType.MaxWeight or PersonalRecordType.EstimatedOneRepMax => "kg",
        PersonalRecordType.SessionVolume => "kg total",
        _ => string.Empty
    };

    private static string FormatPrType(PersonalRecordType type) => type switch
    {
        PersonalRecordType.MaxWeight => "Max weight",
        PersonalRecordType.EstimatedOneRepMax => "Est. 1RM",
        PersonalRecordType.SessionVolume => "Session volume",
        _ => type.ToString()
    };
}

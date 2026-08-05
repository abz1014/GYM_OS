using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's transformation timeline (blueprint Phase 11) — a merge of body measurements,
/// progress photos, achieved goals, personal records, and unlocked achievements into one
/// chronological, append-only feed, newest first. A pure read composition: no new source table, no
/// business rule (unlike Recovery/Recommendation, there's nothing to classify — just merge and sort),
/// so this lives entirely in the query rather than a domain policy. Self-scoped via MyMemberResolver.
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
            .Select(r => new { r.ExerciseId, r.Type, r.Value, r.AchievedAt })
            .ToListAsync(cancellationToken);
        if (records.Count > 0)
        {
            var exerciseIds = records.Select(r => r.ExerciseId).Distinct().ToList();
            var exerciseNames = await db.Exercises.AsNoTracking()
                .Where(e => exerciseIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name })
                .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);
            entries.AddRange(records.Select(r => new MyTimelineEntryDto(
                "PersonalRecord",
                r.AchievedAt,
                $"New PR: {exerciseNames.GetValueOrDefault(r.ExerciseId, "Unknown")}",
                $"{FormatPrType(r.Type)}: {r.Value}",
                null)));
        }

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

    private static string FormatPrType(PersonalRecordType type) => type switch
    {
        PersonalRecordType.MaxWeight => "Max weight",
        PersonalRecordType.EstimatedOneRepMax => "Est. 1RM",
        PersonalRecordType.SessionVolume => "Session volume",
        _ => type.ToString()
    };
}

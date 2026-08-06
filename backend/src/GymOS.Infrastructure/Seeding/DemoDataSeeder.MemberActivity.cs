using GymOS.Domain.Attendance;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Seeding;

/// <summary>
/// Realistic member training history — the data every member-facing surface reads from.
///
/// Before this existed the demo had 500 randomly scattered check-ins (about two visits per member in
/// two months, which is not a gym) and workout logs for exactly ONE member. Every member screen the
/// Experience Engine drives — streaks, mastery, records, leaderboards, celebrations — therefore
/// rendered empty for 214 of 215 members, and the whole engine was undemonstrable.
///
/// Two things this deliberately does NOT do:
///
/// 1. It does not log a workout for every visit. Most people who go to a gym never record anything,
///    which is precisely the problem the product is trying to solve — so the seed reproduces it. That
///    keeps the capture-rate baseline (logged sessions ÷ visits) honest and leaves the one-tap
///    logging work something real to move.
/// 2. It does not hand-write XP, personal records, mastery or achievements. Every seeded session goes
///    through <see cref="WorkoutLog.RaiseLogged"/>, the same domain event a real workout raises, so
///    the engine computes all of it. Fabricating those rows here would let the seed drift from the
///    live rules the moment either changed.
/// </summary>
public partial class DemoDataSeeder
{
    /// <summary>How a member uses the gym. Drives visit cadence and whether they log anything.</summary>
    private enum MemberPersona
    {
        /// <summary>Trains consistently and records most sessions — the engaged minority.</summary>
        Committed,
        /// <summary>Trains consistently but rarely records anything. The largest real-world group.</summary>
        Regular,
        /// <summary>Turns up occasionally, logs almost nothing.</summary>
        Casual,
        /// <summary>Was training, then stopped several weeks ago — gives the at-risk reports real subjects.</summary>
        Lapsed,
        /// <summary>Joined but never really started.</summary>
        Dormant
    }

    private const int HistoryDays = 90;

    /// <summary>
    /// Visits and training history for the whole member base, in one pass so a workout can never land
    /// on a day the member wasn't in the building.
    /// </summary>
    /// <param name="curatedMemberId">The linked demo-member login, whose most recent sessions are
    /// hand-built by SeedDemoMemberIntelligenceDataAsync to produce a specific plateau and a specific
    /// progression for the portal's coaching cards. They still get generated history — an empty
    /// account is the worst thing to demo — but it stops short of that curated window so the
    /// hand-built sessions remain the latest ones and the cards keep saying what they were meant to.</param>
    private async Task SeedMemberActivityAsync(
        Guid tenantId, List<Member> members, Guid? curatedMemberId, CancellationToken cancellationToken)
    {
        var exercises = await db.Exercises.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        if (exercises.Count == 0)
        {
            return; // Exercise library hasn't been seeded — nothing to train with.
        }

        var attending = members.Where(m => m.Status is MemberStatus.Active or MemberStatus.Frozen).ToList();
        if (attending.Count == 0)
        {
            return;
        }

        // Fixed seed: a demo that changes shape every reseed can't be talked through twice.
        var rng = new Random(20260806);
        int[] peakHours = [6, 7, 8, 17, 18, 19, 20];
        var today = DateTimeOffset.UtcNow;

        // A running "best so far" per member+exercise, so weights climb over the 90 days and the
        // progression, plateau and personal-record logic all have a real curve to read.
        var workingWeights = new Dictionary<(Guid Member, Guid Exercise), decimal>();
        var logsSinceSave = 0;

        foreach (var member in attending)
        {
            var isCurated = curatedMemberId is Guid curated && member.Id == curated;
            var persona = isCurated ? MemberPersona.Committed : AssignPersona(rng);
            var (visitsPerWeek, logRate, stoppedWeeksAgo) = PersonaProfile(persona);
            if (isCurated)
            {
                // Rich history, but nothing inside the last two weeks — that window belongs to the
                // hand-built plateau/progression sessions.
                logRate = 90;
                stoppedWeeksAgo = 2;
            }

            if (visitsPerWeek == 0)
            {
                continue;
            }

            // Each member trains on their own fixed weekdays rather than at random, which is what
            // makes a weekly streak meaningful instead of noise.
            var trainingDays = PickTrainingDays(rng, visitsPerWeek);

            for (var daysAgo = HistoryDays; daysAgo >= 0; daysAgo--)
            {
                var date = today.AddDays(-daysAgo);
                if (!trainingDays.Contains(date.DayOfWeek))
                {
                    continue;
                }

                // Lapsed members simply stop partway through the window.
                if (stoppedWeeksAgo is int stopped && daysAgo < stopped * 7)
                {
                    continue;
                }

                // Real attendance is never perfect — miss roughly one session in six.
                if (rng.Next(100) < 15)
                {
                    continue;
                }

                var hour = rng.Next(100) < 70 ? peakHours[rng.Next(peakHours.Length)] : rng.Next(6, 21);
                // Built as an explicit UTC DateTimeOffset — see SeedAttendanceAsync for why .Date is
                // unsafe here (Npgsql rejects a non-zero offset on timestamptz).
                var checkIn = new DateTimeOffset(date.Year, date.Month, date.Day, hour, rng.Next(0, 60), 0, TimeSpan.Zero);

                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    TenantId = tenantId,
                    BranchId = member.BranchId,
                    MemberId = member.Id,
                    CheckInAt = checkIn,
                    CheckOutAt = checkIn.AddMinutes(rng.Next(35, 95)),
                    Method = rng.Next(100) < 85 ? AttendanceMethod.QrSimulated : AttendanceMethod.Manual
                });

                // The visit happened. Whether it was RECORDED is the product problem.
                if (rng.Next(100) >= logRate)
                {
                    continue;
                }

                var log = new WorkoutLog { MemberId = member.Id, LoggedAt = checkIn.AddMinutes(rng.Next(30, 80)) };
                foreach (var exercise in PickSessionExercises(rng, exercises))
                {
                    var key = (member.Id, exercise.Id);
                    if (!workingWeights.TryGetValue(key, out var weight))
                    {
                        weight = StartingWeightFor(exercise, rng);
                    }

                    // Add a little load about a third of the time. Everyone else is holding steady,
                    // which is what gives the plateau detector something true to find.
                    if (rng.Next(100) < 35)
                    {
                        weight += 2.5m;
                    }

                    workingWeights[key] = weight;

                    log.Entries.Add(new WorkoutLogEntry
                    {
                        ExerciseId = exercise.Id,
                        SetsCompleted = rng.Next(3, 6),
                        RepsCompleted = rng.Next(6, 13),
                        // Bodyweight movements carry no load, and the engine must handle that.
                        WeightKg = exercise.Equipment == "Bodyweight" ? null : weight
                    });
                }

                // The same event a real logged workout raises. This is what turns a row into XP,
                // records, mastery, achievements, streaks and challenge progress.
                log.RaiseLogged();
                db.WorkoutLogs.Add(log);

                // Saving in batches keeps the change tracker (and the event cascade it dispatches per
                // save) to a workable size; one save at the end would hold tens of thousands of
                // entities and fire every handler in a single pass.
                if (++logsSinceSave >= 40)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    logsSinceSave = 0;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var visits = await db.AttendanceRecords.IgnoreQueryFilters().CountAsync(a => a.TenantId == tenantId, cancellationToken);
        var logged = await db.WorkoutLogs.IgnoreQueryFilters().CountAsync(cancellationToken);
        logger.LogInformation(
            "Seeded member activity: {Visits} visits and {Logged} logged sessions over {Days} days.",
            visits, logged, HistoryDays);
    }

    /// <summary>
    /// Roughly the shape of a real gym: a committed minority, a large group who turn up without ever
    /// recording anything, and a meaningful tail who drifted away.
    /// </summary>
    private static MemberPersona AssignPersona(Random rng) => rng.Next(100) switch
    {
        < 18 => MemberPersona.Committed,
        < 45 => MemberPersona.Regular,
        < 70 => MemberPersona.Casual,
        < 88 => MemberPersona.Lapsed,
        _ => MemberPersona.Dormant
    };

    /// <summary>Visits per week, the percent chance a visit gets logged, and when they stopped (if they did).</summary>
    private static (int VisitsPerWeek, int LogRate, int? StoppedWeeksAgo) PersonaProfile(MemberPersona persona)
        => persona switch
        {
            MemberPersona.Committed => (4, 80, null),
            MemberPersona.Regular => (3, 12, null),
            MemberPersona.Casual => (1, 5, null),
            MemberPersona.Lapsed => (3, 20, 4),
            _ => (0, 0, null)
        };

    private static HashSet<DayOfWeek> PickTrainingDays(Random rng, int count)
    {
        // Weekday-leaning, which is how most gyms actually fill up.
        DayOfWeek[] pool =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
        ];

        var days = new HashSet<DayOfWeek>();
        while (days.Count < count)
        {
            days.Add(pool[rng.Next(pool.Length)]);
        }

        return days;
    }

    /// <summary>Two to four movements, which is what a real session looks like written down.</summary>
    private static List<Exercise> PickSessionExercises(Random rng, List<Exercise> exercises)
    {
        var wanted = Math.Min(rng.Next(2, 5), exercises.Count);
        var chosen = new List<Exercise>(wanted);
        var used = new HashSet<Guid>();

        while (chosen.Count < wanted)
        {
            var candidate = exercises[rng.Next(exercises.Count)];
            if (used.Add(candidate.Id))
            {
                chosen.Add(candidate);
            }
        }

        return chosen;
    }

    /// <summary>Plausible opening loads so the 90-day curve lands somewhere believable.</summary>
    private static decimal StartingWeightFor(Exercise exercise, Random rng) => exercise.Equipment switch
    {
        "Bodyweight" => 0m,
        "Barbell" => 40m + rng.Next(0, 8) * 2.5m,
        "Dumbbell" => 10m + rng.Next(0, 6) * 2.5m,
        "Cable Machine" => 20m + rng.Next(0, 6) * 2.5m,
        "Machine" => 25m + rng.Next(0, 8) * 2.5m,
        _ => 15m + rng.Next(0, 5) * 2.5m
    };
}

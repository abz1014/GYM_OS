using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Queries;
using GymOS.Application.Modules.Experience.Queries;
using GymOS.Domain.Experience;
using GymOS.Domain.Trainers;
using GymOS.Domain.Workouts;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Classes;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member home screen in one request: weekly ring, streak, today's class, and the top nudge.
///
/// Replaces five separate client calls whose results the browser then had to combine — including
/// deriving "sessions this week" itself, which it got wrong for anyone training without weights (it
/// counted lifting volume, so a bodyweight or cardio day scored zero and didn't count). The counting
/// rule now lives in WeeklyGoalPolicy, server-side, shared with everything else that talks about
/// weeks. Self-scoped via MyMemberResolver; no member id is accepted from the caller.
/// </summary>
public record GetMyTodayQuery : IQuery<MyTodayDto>;

public class GetMyTodayQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, ISender sender)
    : IRequestHandler<GetMyTodayQuery, MyTodayDto>
{
    public async Task<MyTodayDto> Handle(GetMyTodayQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        // The raw configured id, not TimeZoneInfo.Id — it is sent to the browser and must stay IANA
        // whichever OS the API happens to be on. See MyMemberResolver.ResolveGymZoneIdAsync.
        var zoneId = await MyMemberResolver.ResolveGymZoneIdAsync(db, memberId, cancellationToken);
        var zone = GymDay.ZoneOrUtc(zoneId);
        var now = dateTimeProvider.UtcNow;
        var today = GymDay.Of(now, zone);

        var firstName = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => m.FirstName)
            .FirstAsync(cancellationToken);

        // One pull of the member's workout dates feeds BOTH the ring and the streak flame, so the two
        // numbers shown side by side are computed from exactly the same rows. DateTimeOffset can't be
        // bucketed to a date in SQL on SQLite, so reduce in memory — the pattern every MEE query over
        // WorkoutLogs already uses.
        var workoutDates = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == memberId)
                .Select(w => w.LoggedAt)
                .ToListAsync(cancellationToken))
            .Select(loggedAt => GymDay.Of(loggedAt, zone))
            .ToList();

        // An absent preference row means "never customised" and reads as the default, so members who
        // predate this setting need no backfill.
        var goal = await db.MemberTrainingPreferences.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (int?)p.WeeklySessionGoal)
            .FirstOrDefaultAsync(cancellationToken) ?? WeeklyGoalPolicy.DefaultWeeklySessionGoal;

        var sessionsThisWeek = WeeklyGoalPolicy.SessionsThisWeek(workoutDates, today);

        // Deliberately NOT reusing GetMyClassBookingsQuery: its "still upcoming" filter compares a
        // DateTimeOffset across the booking->session join, which the SQLite test provider cannot
        // translate, so composing it here would make this whole screen untestable. The status rule is
        // restated (it is one clause) and the time filter runs in memory — the same trade every other
        // query over DateTimeOffset in this codebase makes. A member's own bookings are a small set.
        var myBookings = await db.ClassBookings.AsNoTracking()
            .Where(b => b.MemberId == memberId
                        && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted))
            .Select(b => new MyClassBookingDto(
                b.Id,
                b.ClassSessionId,
                b.ClassSession!.ClassType!.Name,
                b.ClassSession.ClassType.ColorHex,
                b.ClassSession.Trainer == null ? null : b.ClassSession.Trainer.User!.FirstName + " " + b.ClassSession.Trainer.User.LastName,
                b.ClassSession.StartsAt,
                b.ClassSession.DurationMinutes,
                b.ClassSession.Location,
                b.Status))
            .ToListAsync(cancellationToken);

        var nextClassToday = myBookings
            .Where(b => b.StartsAt >= now && GymDay.Of(b.StartsAt, zone) == today)
            .OrderBy(b => b.StartsAt)
            .FirstOrDefault();

        // The insight engine composes what the other policies already computed rather than
        // recomputing any of it — recovery classifies muscle groups, the overload policy knows who
        // has earned a heavier attempt, mastery knows the weakest area, the passport knows what has
        // been dropped. Each fed a different screen and none was ranked against the others.
        var recovery = await sender.Send(new GetMyRecoveryQuery(), cancellationToken);
        var suggestions = await sender.Send(new GetMyWorkoutSuggestionsQuery(), cancellationToken);
        var mastery = await sender.Send(new GetMyMasteryQuery(), cancellationToken);
        var passport = await sender.Send(new GetMyPassportQuery(), cancellationToken);

        var fatigued = recovery.MuscleGroups
            .FirstOrDefault(g => g.Status == nameof(RecoveryStatus.Fatigued));
        var ready = suggestions
            .FirstOrDefault(s => s.Suggestion == OverloadSuggestion.ReadyToIncreaseWeight);
        // ConsiderDeload means weight or reps came DOWN — see ProgressiveOverloadPolicy. It used to
        // be handed to an insight that told the member the lift "hasn't moved", which is the one
        // thing this verdict never means. Named for what it is now.
        var easedOff = suggestions
            .FirstOrDefault(s => s.Suggestion == OverloadSuggestion.ConsiderDeload);
        var weakest = mastery.MuscleGroups
            .OrderBy(g => g.MasteryPercent).ThenBy(g => g.Name)
            .FirstOrDefault();

        var insights = TrainingInsightPolicy.Rank(new TrainingInsightSignals(
            fatigued?.MuscleGroup,
            fatigued?.Reason,
            ready?.ExerciseName,
            ready?.SuggestedNextWeightKg,
            easedOff?.ExerciseName,
            passport.GoneQuiet
                .Select(q => new QuietMovement(q.ExerciseName, q.MuscleGroup, q.DaysSinceLastTrained ?? 0))
                .ToList(),
            weakest?.Name,
            // Momentum is left unset here: it needs a gain measured across the member's record
            // history, which this screen does not load. Reporting it from a single current best
            // would be a made-up number, and an absent signal produces no insight by design.
            null, 0, 0));

        // The member's own check-ins, reduced in memory for the same reason the workout dates above
        // are: a DateTimeOffset cannot be bucketed to a date or range-filtered in SQL on SQLite, so
        // narrowing to today has to happen after the read. AttendanceRecord is IBranchScoped, so the
        // rows are already confined to this tenant and branch before the member filter applies.
        var visits = (await db.AttendanceRecords.AsNoTracking()
                .Where(a => a.MemberId == memberId)
                .Select(a => new { a.CheckInAt, a.CheckOutAt })
                .ToListAsync(cancellationToken))
            .Select(a => (a.CheckInAt, a.CheckOutAt))
            .ToList();

        var visit = VisitPolicy.Classify(visits, workoutDates, now, zone);

        // What the member has ahead of them. Every input is already loaded or already computed for
        // something else on this screen, so looking forward costs one extra read rather than a
        // second pass over the member's history.
        var nextBooking = myBookings.Where(b => b.StartsAt >= now).OrderBy(b => b.StartsAt).FirstOrDefault();

        var joinedChallenge = (await sender.Send(new GetMyChallengesQuery(), cancellationToken))
            .Where(c => c.Joined && !c.IsCompleted)
            .OrderBy(c => c.TargetWorkoutCount - c.MyWorkoutCount)
            .FirstOrDefault();

        var totalXp = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (long?)p.TotalXp)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
        var level = XpPolicy.LevelForXp(totalXp);

        /*
         * Converted to the gym's clock before the policy sees them.
         *
         * AnticipationPolicy formats a DateTimeOffset in whatever offset the value carries, and class
         * times are stored at +00:00 — so an 18:00 class at a New York gym came back as "Today at
         * 6:00 PM" when the gym's own clock says 2:00 PM. The same conversion also fixes the day
         * bucketing inside WhenPhrase, which subtracts .Date values: a 9pm-local class stored as
         * tomorrow in UTC read as "Tomorrow" to a member standing in the gym that evening.
         *
         * Done here rather than inside the policy because the policy is a pure rule that takes the
         * times it is given, and only the query knows which gym is being asked about.
         */
        var comingNow = TimeZoneInfo.ConvertTime(now, zone);
        var comingClassAt = nextBooking is null ? (DateTimeOffset?)null : TimeZoneInfo.ConvertTime(nextBooking.StartsAt, zone);

        var coming = AnticipationPolicy.Next(
            new AnticipationSignals(
                nextBooking?.ClassTypeName,
                comingClassAt,
                joinedChallenge?.Name,
                joinedChallenge is null ? 0 : Math.Max(0, joinedChallenge.TargetWorkoutCount - joinedChallenge.MyWorkoutCount),
                level.Level + 1,
                level.XpForNextLevel - level.XpIntoLevel,
                XpPolicy.AwardFor(XpReason.WorkoutCompleted)),
            comingNow);

        /*
         * Whether the member's coach has said something they have not opened.
         *
         * It rides on this query rather than getting its own because the home screen is the first
         * thing a member sees and the badge has to be right there — a notification the member has to
         * navigate to find is the exact problem it exists to solve. Only the trainer's own messages
         * count: a member's unread outbound message means the coach hasn't opened it, which is not
         * something to badge the member about.
         */
        var unreadFromCoach = await db.CoachMessages
            .CountAsync(c => c.MemberId == memberId && c.Author == CoachMessageAuthor.Trainer && c.ReadAt == null,
                cancellationToken);

        return new MyTodayDto(
            firstName,
            sessionsThisWeek,
            goal,
            WeeklyGoalPolicy.RemainingSessions(sessionsThisWeek, goal),
            WeeklyGoalPolicy.IsGoalMet(sessionsThisWeek, goal),
            StreakCalculator.CurrentWeeklyStreak(workoutDates, today),
            WeeklyGoalPolicy.DaysTrainedThisWeek(workoutDates, today),
            nextClassToday,
            insights.Select(i => new MyInsightDto(i.Kind.ToString(), i.Title, i.Detail)).ToList(),
            new MyVisitDto(visit.State.ToString(), visit.CheckedInAt, visit.SessionRecorded, visit.NeedsRecording),
            coming is null ? null : new MyAnticipationDto(coming.Value.Kind.ToString(), coming.Value.Title, coming.Value.Detail),
            unreadFromCoach,
            zoneId);
    }
}

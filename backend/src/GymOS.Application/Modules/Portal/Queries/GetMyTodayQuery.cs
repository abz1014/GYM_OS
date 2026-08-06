using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Queries;
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
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
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

        // Recommendations ARE reused — the ranking is real logic and translates fine. Same ISender
        // composition GetMyRecommendationsQuery itself uses for its own inputs.
        var recommendations = await sender.Send(new GetMyRecommendationsQuery(), cancellationToken);

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

        return new MyTodayDto(
            firstName,
            sessionsThisWeek,
            goal,
            WeeklyGoalPolicy.RemainingSessions(sessionsThisWeek, goal),
            WeeklyGoalPolicy.IsGoalMet(sessionsThisWeek, goal),
            StreakCalculator.CurrentWeeklyStreak(workoutDates, today),
            nextClassToday,
            recommendations.FirstOrDefault(),
            new MyVisitDto(visit.State.ToString(), visit.CheckedInAt, visit.SessionRecorded, visit.NeedsRecording));
    }
}

using GymOS.Application.Modules.Experience.Dtos;

namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>
/// Everything the member home screen shows, in one round trip.
///
/// An aggregate rather than five separate calls because the screen answers a single question — "am I
/// on track this week?" — and answering it from five independently-cached responses meant the ring,
/// the streak and the nudge could each be from a different moment, and the browser had to derive the
/// week and the session count itself to stitch them together. Those derivations are now server-side
/// (see WeeklyGoalPolicy), so every surface reports the same week the same way.
/// </summary>
/// <param name="NextClassToday">The member's next class starting later today, if any — already
/// filtered server-side, so the client renders it or doesn't rather than deciding what "today" means.</param>
/// <param name="TopRecommendation">The single highest-priority coaching nudge, or null when the
/// engine has nothing worth saying. The home screen shows at most one on purpose.</param>
/// <param name="Visit">Today's visit as the turnstile already knows it. Lets the screen speak from
/// what the gym has on record instead of greeting someone who just finished training as though they
/// had never arrived. See VisitPolicy.</param>
public record MyTodayDto(
    string FirstName,
    int SessionsThisWeek,
    int WeeklySessionGoal,
    int RemainingSessions,
    bool GoalMet,
    int WorkoutStreakWeeks,
    MyClassBookingDto? NextClassToday,
    MyRecommendationDto? TopRecommendation,
    MyVisitDto Visit);

/// <summary>
/// Today's gym visit. Always present; <paramref name="State"/> is "None" when the member has not
/// checked in today.
/// </summary>
/// <param name="State">None, InGym or Visited.</param>
/// <param name="NeedsRecording">They were here and nothing was written down — the gap between a
/// visit and a session, which is most visits at industry baseline.</param>
public record MyVisitDto(string State, DateTimeOffset? CheckedInAt, bool SessionRecorded, bool NeedsRecording);

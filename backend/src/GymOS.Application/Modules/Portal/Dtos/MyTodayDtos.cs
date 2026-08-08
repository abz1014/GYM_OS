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
/// <param name="Insights">At most two, ranked by what the member can act on today — see
/// TrainingInsightPolicy. Replaces the single unranked nudge this screen used to show, which was
/// whichever recommendation happened to be first rather than the most useful.</param>
/// <param name="Coming">The single nearest thing the member has to look forward to, or null when
/// there genuinely isn't one. Everything else on this screen looks backwards. See AnticipationPolicy.</param>
/// <param name="Visit">Today's visit as the turnstile already knows it. Lets the screen speak from
/// what the gym has on record instead of greeting someone who just finished training as though they
/// had never arrived. See VisitPolicy.</param>
/// <param name="DaysTrainedThisWeek">Seven flags, Monday first, for the day strip under the ring —
/// counted by the same rule from the same rows as <paramref name="SessionsThisWeek"/>, so the strip
/// and the ring above it can never tell two different stories. See WeeklyGoalPolicy.</param>
public record MyTodayDto(
    string FirstName,
    int SessionsThisWeek,
    int WeeklySessionGoal,
    int RemainingSessions,
    bool GoalMet,
    int WorkoutStreakWeeks,
    IReadOnlyList<bool> DaysTrainedThisWeek,
    MyClassBookingDto? NextClassToday,
    IReadOnlyList<MyInsightDto> Insights,
    MyVisitDto Visit,
    MyAnticipationDto? Coming,
    int UnreadCoachMessages);

/// <summary>
/// Today's gym visit. Always present; <paramref name="State"/> is "None" when the member has not
/// checked in today.
/// </summary>
/// <param name="State">None, InGym or Visited.</param>
/// <param name="NeedsRecording">They were here and nothing was written down — the gap between a
/// visit and a session, which is most visits at industry baseline.</param>
public record MyVisitDto(string State, DateTimeOffset? CheckedInAt, bool SessionRecorded, bool NeedsRecording);

/// <summary>What the member has coming. <paramref name="Kind"/> is BookedClass, Challenge or Level.</summary>
public record MyAnticipationDto(string Kind, string Title, string Detail);

/// <summary>One thing the engine noticed. <paramref name="Kind"/> is RecoveryAlert, ReadyForPr,
/// Comeback, Plateau, GoneQuiet or Momentum.</summary>
public record MyInsightDto(string Kind, string Title, string Detail);

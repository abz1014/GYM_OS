using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>On a logged workout, updates the workout-derived projections (personal records + exercise
/// mastery) and, if the session set a record, awards the improvement XP. Runs alongside the XP handler
/// on the same WorkoutLoggedEvent; both are independent and idempotent.</summary>
public class ApplyWorkoutProgressionOnWorkoutLoggedHandler(
    IWorkoutProgressionService progression, IMemberXpService xp)
    : INotificationHandler<DomainEventNotification<WorkoutLoggedEvent>>
{
    public async Task Handle(DomainEventNotification<WorkoutLoggedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var setAnyRecord = await progression.ApplyWorkoutAsync(e.MemberId, e.WorkoutLogId, cancellationToken);

        if (!setAnyRecord)
        {
            return;
        }

        /*
         * XpReason.ProgressiveImprovement was worth 30 XP in XpPolicy from the day it was written and
         * had no call site anywhere, so beating a personal best — the most celebration-worthy thing a
         * member can do — paid nothing at all. This is that wire.
         *
         * ONCE PER WORKOUT, not once per record, and the difference is not small. A record is written
         * per (exercise, PersonalRecordType), PersonalRecordPolicy.Beats compares against a prior best
         * of 0 for a movement never logged before, and there are three record types. A first session
         * across five exercises therefore sets fifteen records: at 30 XP each that is 450 XP — nine
         * workouts' worth — for one visit, and it would arrive on the day a member has done least to
         * earn it. Keyed on the workout instead, an improving session is worth 30 on top of the 50 the
         * workout already pays.
         *
         * SourceId is the workout log, which buys two things for free. The unique index on
         * (MemberId, SourceType, SourceId, Reason) makes a replay a no-op without a new column, and
         * because ProgressiveImprovement is a different Reason from WorkoutCompleted, the two awards
         * for the same workout coexist. It also unwinds correctly: UndoMyWorkoutCommand deletes XP
         * transactions by SourceId == log.Id, so undoing a session takes back the improvement XP with
         * the rest of it rather than stranding a reward for a session that no longer exists.
         */
        await xp.AwardAsync(
            e.MemberId, XpReason.ProgressiveImprovement, XpSourceType.WorkoutLog, e.WorkoutLogId, cancellationToken);
    }
}

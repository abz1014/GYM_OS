using GymOS.Domain.Common;
using GymOS.Domain.Trainers;

namespace GymOS.Domain.Classes;

/// <summary>
/// A concrete, dated occurrence of a class — the thing members actually book (bookings arrive in
/// Step 2). Most sessions are generated from a ClassSchedule, but ClassScheduleId is nullable so a
/// one-off session (a special workshop, a holiday pop-up) can exist without a recurring rule.
/// ClassTypeId is denormalised here rather than only reachable through the schedule, so a one-off
/// session and a queryable "what type is this" both work without a mandatory schedule join.
/// </summary>
public class ClassSession : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? ClassScheduleId { get; set; }

    public ClassSchedule? ClassSchedule { get; set; }

    public Guid ClassTypeId { get; set; }

    public ClassType? ClassType { get; set; }

    public Guid? TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    /// <summary>The session's start instant. Built from the schedule's date + StartTime; stored as
    /// a wall-clock-in-UTC DateTimeOffset (the app's existing time simplification — real time-zone
    /// handling is a later concern), consistent with AttendanceRecord.CheckInAt etc.</summary>
    public DateTimeOffset StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }

    public string? Location { get; set; }

    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;
}

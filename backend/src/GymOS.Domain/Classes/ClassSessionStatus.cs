namespace GymOS.Domain.Classes;

public enum ClassSessionStatus
{
    /// <summary>Materialised and bookable.</summary>
    Scheduled,

    /// <summary>Called off (holiday, instructor out) — existing bookings should be released.</summary>
    Cancelled,

    /// <summary>The session's start time has passed and it ran.</summary>
    Completed
}

namespace GymOS.Domain.Classes;

public enum ClassBookingStatus
{
    /// <summary>Holds a confirmed spot (counts against capacity).</summary>
    Booked,

    /// <summary>Session was full at booking time — promoted to Booked (FIFO) when a spot frees up.</summary>
    Waitlisted,

    /// <summary>The member attended — still occupies a spot.</summary>
    CheckedIn,

    /// <summary>The member had a confirmed spot but didn't attend (recorded after the session).</summary>
    NoShow,

    /// <summary>Released — frees a spot and can trigger a waitlist promotion.</summary>
    Cancelled
}

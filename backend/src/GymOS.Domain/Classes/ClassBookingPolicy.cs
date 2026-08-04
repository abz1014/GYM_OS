namespace GymOS.Domain.Classes;

/// <summary>
/// The capacity rule for class bookings, isolated as a pure function so the "when is a session
/// full" decision is defined once and unit-testable without a database — the same reasoning behind
/// keeping ClassSessionPlanner pure. A spot is "occupied" by a Booked or CheckedIn booking;
/// Waitlisted/NoShow/Cancelled never occupy a spot.
/// </summary>
public static class ClassBookingPolicy
{
    /// <summary>Whether a given status counts against the session's capacity.</summary>
    public static bool Occupies(ClassBookingStatus status) =>
        status is ClassBookingStatus.Booked or ClassBookingStatus.CheckedIn;

    /// <summary>The status a brand-new booking should take: Booked if a spot is free, else Waitlisted.</summary>
    public static ClassBookingStatus StatusForNewBooking(int occupiedCount, int capacity) =>
        occupiedCount < capacity ? ClassBookingStatus.Booked : ClassBookingStatus.Waitlisted;

    /// <summary>Whether cancelling one occupied booking opens a spot a waitlisted member can take.</summary>
    public static bool CanPromoteFromWaitlist(int occupiedCountAfterCancel, int capacity) =>
        occupiedCountAfterCancel < capacity;
}

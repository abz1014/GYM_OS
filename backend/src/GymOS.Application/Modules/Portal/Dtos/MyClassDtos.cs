using GymOS.Domain.Classes;

namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>A bookable session as the member sees it, including whether spots remain and — the
/// bit staff views don't need — this member's own booking status on it (null if not booked).</summary>
public record MyClassSessionDto(
    Guid SessionId, string ClassTypeName, string? ColorHex, string? TrainerName, DateTimeOffset StartsAt,
    int DurationMinutes, int Capacity, string? Location, int BookedCount, bool IsFull,
    ClassBookingStatus? MyBookingStatus, Guid? MyBookingId);

/// <summary>
/// One of the member's own bookings, with the session details needed to show "your upcoming classes".
///
/// <see cref="WaitlistPosition"/> is 1-based and null unless the booking is Waitlisted. A bare
/// "Waitlisted" told the member they were in a queue without telling them where in it — and the two
/// ends of that queue call for opposite decisions, since first in line should keep the slot free and
/// eleventh should not. Computed by WaitlistPositionResolver from the same BookedAt order the
/// promotion rule uses, so the number shown is the actual queue rather than a second opinion of it.
/// </summary>
public record MyClassBookingDto(
    Guid BookingId, Guid SessionId, string ClassTypeName, string? ColorHex, string? TrainerName,
    DateTimeOffset StartsAt, int DurationMinutes, string? Location, ClassBookingStatus Status,
    int? WaitlistPosition);

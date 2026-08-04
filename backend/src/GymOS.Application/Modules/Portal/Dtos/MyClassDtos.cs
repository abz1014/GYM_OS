using GymOS.Domain.Classes;

namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>A bookable session as the member sees it, including whether spots remain and — the
/// bit staff views don't need — this member's own booking status on it (null if not booked).</summary>
public record MyClassSessionDto(
    Guid SessionId, string ClassTypeName, string? ColorHex, string? TrainerName, DateTimeOffset StartsAt,
    int DurationMinutes, int Capacity, string? Location, int BookedCount, bool IsFull,
    ClassBookingStatus? MyBookingStatus, Guid? MyBookingId);

/// <summary>One of the member's own bookings, with the session details needed to show "your upcoming classes".</summary>
public record MyClassBookingDto(
    Guid BookingId, Guid SessionId, string ClassTypeName, string? ColorHex, string? TrainerName,
    DateTimeOffset StartsAt, int DurationMinutes, string? Location, ClassBookingStatus Status);

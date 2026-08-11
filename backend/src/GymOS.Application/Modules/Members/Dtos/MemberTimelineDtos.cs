namespace GymOS.Application.Modules.Members.Dtos;

/// <summary>
/// One thing that happened to a member, from whichever module recorded it.
/// </summary>
/// <param name="Kind">Which module it came from, so the UI can icon it. Never shown raw.</param>
/// <param name="At">
/// When. Sources that store a calendar date rather than a timestamp — invoices, memberships,
/// measurements, diet plans — land at midnight, so their order against a same-day check-in is
/// approximate. That is worth saying out loud and not worth hiding the event over; the member panel
/// has made the same trade since it was written.
/// </param>
/// <param name="Title">The event, already phrased for a person.</param>
/// <param name="Detail">The one extra fact worth carrying, or null when there isn't one.</param>
public record MemberTimelineEntryDto(
    string Kind,
    DateTimeOffset At,
    string Title,
    string? Detail);

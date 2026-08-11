namespace GymOS.Application.Modules.Members.Dtos;

/// <summary>
/// One thing that happened to a member, from whichever module recorded it.
/// </summary>
/// <param name="Kind">Which module it came from, so the UI can icon it. Never shown raw.</param>
/// <param name="At">
/// When. Sources that store a calendar date rather than a timestamp land at midnight UTC, so their
/// order against a same-day check-in is approximate. That is worth saying out loud and not worth
/// hiding the event over; the member panel has made the same trade since it was written.
/// </param>
/// <param name="IsDateOnly">
/// True when the source recorded a DATE and no time — invoices, memberships, measurements, diet
/// plans. The client cannot infer this and must not guess: midnight UTC is a real instant, and once
/// the browser converts it the fake time is not even recognisable as midnight. Rendered without an
/// hour, and with its calendar date taken from the UTC parts, a measurement stops claiming it was
/// taken at 5am and stops sliding to the previous day for anyone west of UTC.
/// </param>
/// <param name="Title">The event, already phrased for a person.</param>
/// <param name="Detail">The one extra fact worth carrying, or null when there isn't one.</param>
public record MemberTimelineEntryDto(
    string Kind,
    DateTimeOffset At,
    bool IsDateOnly,
    string Title,
    string? Detail);

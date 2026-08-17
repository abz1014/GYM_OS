using GymOS.Domain.Billing;
using GymOS.Domain.Classes;
using GymOS.Domain.Notifications;

namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>
/// One of the member's own invoices, as the member needs to read it: what it was for, when it is
/// due, and how much of it is settled. Deliberately narrower than the staff
/// <c>InvoiceListItemDto</c> — a member has no use for MemberId/MemberName (there is only ever one
/// member in this list, themselves) and surfacing them would only widen what the portal returns.
/// </summary>
public record MyInvoiceDto(
    Guid Id, string InvoiceNumber, DateOnly IssueDate, DateOnly DueDate, InvoiceStatus Status,
    decimal TotalAmount, decimal PaidAmount, string Currency);

/// <summary>
/// A notification this gym addressed to this member. <see cref="Body"/> is nullable because the
/// content lives on the template, and a template with an empty body is a real (if unhelpful) state —
/// the member gets a title and nothing else rather than an empty string pretending to be a message.
/// </summary>
public record MyNotificationDto(
    Guid Id, string Title, string? Body, NotificationChannel Channel, DateTimeOffset OccurredAt);

/// <summary>
/// A class the member has already been to — or was booked onto and missed. Carries no booking id
/// because nothing can be done to a past booking; this is a record, not a control surface.
/// </summary>
public record MyClassHistoryDto(
    string ClassTypeName, DateTimeOffset StartsAt, int DurationMinutes, ClassBookingStatus Status);

/// <summary>
/// Where the member actually trains, and how to reach the people who run it.
///
/// This exists because the member-facing copy says "ask the front desk" in half a dozen places while
/// the app itself refused to say where the desk is, or what its number was. The branch is the
/// member's own; the support contacts come from the gym profile and are null when the gym has not
/// filled them in — an absent phone number must read as absent, not as an empty string the UI then
/// renders as a broken "call us" link.
/// </summary>
public record MyGymDto(
    string BranchName, string AddressLine, string City, string Country,
    string? SupportEmail, string? SupportPhone);

/// <summary>
/// The body of an emergency-contact edit. Exists because the contact's id belongs in the route, not
/// in the payload — a body-supplied id on a member-scoped endpoint is exactly the shape this whole
/// controller avoids, and two ids that could disagree is a bug waiting for a mismatched client.
/// </summary>
public record MyEmergencyContactInput(string Name, string Phone, string Relationship);

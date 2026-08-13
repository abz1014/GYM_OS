using GymOS.Domain.Members;

namespace GymOS.Application.Modules.Members.Dtos;

public record MemberListItemDto(
    Guid Id,
    string MemberCode,
    string FullName,
    string Email,
    string? Phone,
    string? ProfilePhotoUrl,
    MemberStatus Status,
    DateOnly JoinDate);

public record EmergencyContactDto(Guid Id, string Name, string Relationship, string Phone, string? Email);

public record MedicalNoteDto(Guid Id, string Note, Guid? RecordedByUserId, DateTimeOffset RecordedAt);

public record MemberMeasurementDto(
    Guid Id, DateOnly MeasuredOn, decimal? WeightKg, decimal? BodyFatPercentage,
    decimal? ChestCm, decimal? WaistCm, decimal? HipCm, decimal? ArmCm, decimal? ThighCm, string? Notes);

public record ProgressPhotoDto(Guid Id, string PhotoUrl, DateTimeOffset TakenAt, string? Notes);

// FreezeDaysUsed + PlanMaxFreezeDays travel together so the freeze dialog can state the remaining
// allowance BEFORE a request is made — until these existed, the only carrier of that number was the
// 400 message a refused freeze came back with.
public record MemberMembershipDto(
    Guid Id, Guid MembershipPlanId, string MembershipPlanName, DateOnly StartDate, DateOnly EndDate,
    MemberMembershipStatus Status, bool AutoRenew, DateOnly? FreezeStartDate, DateOnly? FreezeEndDate,
    int FreezeDaysUsed, int? PlanMaxFreezeDays,
    decimal PricePaid, string Currency, Guid? InvoiceId, string? CancellationReason);

public record MemberDetailDto(
    Guid Id,
    string MemberCode,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Email,
    string? Phone,
    string? Address,
    string? ProfilePhotoUrl,
    DateOnly JoinDate,
    MemberStatus Status,
    string QrCodeToken,
    Guid BranchId,
    Guid? ReferredByMemberId,
    string? ReferredByName,
    // The member's ACTIVE coaching pairing, or null when they train on their own. Part of the member's
    // service profile rather than the trainer module's roster — the front desk answers "who coaches
    // me?" under members.view without needing trainers.view.
    Guid? AssignedTrainerId,
    string? AssignedTrainerName,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    IReadOnlyList<MedicalNoteDto> MedicalNotes,
    IReadOnlyList<MemberMeasurementDto> Measurements,
    IReadOnlyList<ProgressPhotoDto> ProgressPhotos,
    IReadOnlyList<MemberMembershipDto> MemberMemberships);

/// <summary>
/// What happened to one member in a batch action. <c>Reason</c> is populated only on failure and is
/// written for the person reading it — "This plan allows at most 7 freeze day(s); 14 requested."
/// </summary>
public record BatchMemberOutcomeDto(Guid MemberId, string? MemberName, bool Succeeded, string? Reason);

/// <summary>
/// The result of a batch action, reported per member.
///
/// The counts exist so the caller can say "14 of 20 frozen" without walking the list, and the list
/// exists so it can say WHICH six and why. A batch that returned only a count would leave staff
/// believing an action applied to a selection it partly skipped — which is the failure mode this
/// whole shape is designed to prevent. See BatchFreezeMembershipsCommand.
/// </summary>
public record BatchResultDto(int Requested, int Succeeded, int Failed, IReadOnlyList<BatchMemberOutcomeDto> Outcomes)
{
    public static BatchResultDto From(IReadOnlyList<BatchMemberOutcomeDto> outcomes)
    {
        var succeeded = outcomes.Count(o => o.Succeeded);
        return new BatchResultDto(outcomes.Count, succeeded, outcomes.Count - succeeded, outcomes);
    }
}

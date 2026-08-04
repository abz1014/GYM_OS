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

public record MemberMembershipDto(
    Guid Id, Guid MembershipPlanId, string MembershipPlanName, DateOnly StartDate, DateOnly EndDate,
    MemberMembershipStatus Status, bool AutoRenew, DateOnly? FreezeStartDate, DateOnly? FreezeEndDate,
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
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    IReadOnlyList<MedicalNoteDto> MedicalNotes,
    IReadOnlyList<MemberMeasurementDto> Measurements,
    IReadOnlyList<ProgressPhotoDto> ProgressPhotos,
    IReadOnlyList<MemberMembershipDto> MemberMemberships);

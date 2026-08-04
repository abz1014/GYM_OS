using GymOS.Domain.Crm;

namespace GymOS.Application.Modules.Crm.Dtos;

public record LeadListItemDto(
    Guid Id, string FullName, string Email, string? Phone, LeadSource Source, LeadStage Stage,
    Guid? AssignedToUserId, DateTimeOffset CreatedAt);

public record LeadActivityDto(Guid Id, LeadActivityType Type, string Notes, DateTimeOffset? DueDate, DateTimeOffset? CompletedAt);

public record LeadDetailDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone, LeadSource Source, LeadStage Stage,
    Guid BranchId, Guid? AssignedToUserId, Guid? ConvertedMemberId, string? Notes, DateTimeOffset CreatedAt,
    IReadOnlyList<LeadActivityDto> Activities);

public record CrmPipelineSummaryDto(int LeadCount, int FollowUpCount, int TrialCount, int MemberCount, int LostCount, double ConversionRatePercent);

public record TopReferrerDto(Guid MemberId, string FullName, string MemberCode, int ReferralCount);

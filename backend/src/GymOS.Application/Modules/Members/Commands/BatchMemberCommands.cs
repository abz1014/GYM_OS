using FluentValidation;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>
/// How many members one action may touch. Not a performance limit — it is the number above which a
/// mis-click stops being recoverable by hand. The members list pages at 20, so a full page plus a
/// generous multi-page selection fits, and "select all 300 and freeze them" does not.
/// </summary>
public static class BatchLimits
{
    public const int MaxMembersPerBatch = 100;
}

public record BatchFreezeMembershipsCommand(IReadOnlyList<Guid> MemberIds, DateOnly FreezeStartDate, DateOnly FreezeEndDate)
    : ICommand<BatchResultDto>;

public class BatchFreezeMembershipsCommandValidator : AbstractValidator<BatchFreezeMembershipsCommand>
{
    public BatchFreezeMembershipsCommandValidator()
    {
        RuleFor(x => x.MemberIds).NotEmpty().Must(ids => ids.Count <= BatchLimits.MaxMembersPerBatch)
            .WithMessage($"At most {BatchLimits.MaxMembersPerBatch} members can be changed at once.");
        RuleFor(x => x.FreezeEndDate).GreaterThanOrEqualTo(x => x.FreezeStartDate);
    }
}

/// <summary>
/// Freeze the active membership of each selected member, reporting the outcome per member.
///
/// **Partial success is the normal case, not the error case.** <c>MaxFreezeDays</c> is a property of
/// the PLAN, so any selection spanning more than one plan will contain members this freeze is legal
/// for and members it is not. An all-or-nothing batch would therefore refuse the whole action
/// because one person is on the wrong plan, and a silently-partial one would tell staff twenty
/// members were frozen when eleven were — which is worse, because they would not go back and check.
/// So every member gets a row in the result and the caller renders the split.
///
/// Members outside the caller's accessible branches are not merely skipped, they are reported as
/// not found: a batch endpoint must not become an oracle that confirms a member id exists in a
/// branch the caller cannot see.
/// </summary>
public class BatchFreezeMembershipsCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BatchFreezeMembershipsCommand, BatchResultDto>
{
    public async Task<BatchResultDto> Handle(BatchFreezeMembershipsCommand request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var requestedIds = request.MemberIds.Distinct().ToList();
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var members = await db.Members
            .Where(m => requestedIds.Contains(m.Id) && accessibleBranchIds.Contains(m.BranchId))
            .Include(m => m.MemberMemberships)
                .ThenInclude(mm => mm.MembershipPlan)
            .ToListAsync(cancellationToken);

        var byId = members.ToDictionary(m => m.Id);
        var outcomes = new List<BatchMemberOutcomeDto>(requestedIds.Count);

        foreach (var id in requestedIds)
        {
            if (!byId.TryGetValue(id, out var member))
            {
                outcomes.Add(new BatchMemberOutcomeDto(id, null, false, "Member not found."));
                continue;
            }

            var name = member.FirstName + " " + member.LastName;

            // Only an Active membership can be frozen. A PendingActivation one has nothing to pause,
            // and an Expired or Cancelled one would be resurrected into a state it never held.
            var membership = member.MemberMemberships.FirstOrDefault(mm => mm.Status == MemberMembershipStatus.Active);
            if (membership is null)
            {
                outcomes.Add(new BatchMemberOutcomeDto(id, name, false, "No active membership to freeze."));
                continue;
            }

            var maxFreezeDays = membership.MembershipPlan?.MaxFreezeDays ?? MembershipFreezePolicy.NoFreezeAllowance;
            // Same cumulative allowance as the single-membership path — a rule that only one of two
            // callers enforces is not a rule.
            var (allowed, reason) = MembershipFreezePolicy.Evaluate(
                maxFreezeDays, membership.FreezeDaysUsed, membership.StartDate, today,
                request.FreezeStartDate, request.FreezeEndDate);
            if (!allowed)
            {
                outcomes.Add(new BatchMemberOutcomeDto(id, name, false, reason));
                continue;
            }

            membership.FreezeStartDate = request.FreezeStartDate;
            membership.FreezeEndDate = request.FreezeEndDate;
            membership.Status = MemberMembershipStatus.Frozen;
            member.Status = MemberStatus.Frozen;

            outcomes.Add(new BatchMemberOutcomeDto(id, name, true, null));
        }

        // One SaveChanges for the whole batch: the successful rows commit together, and a failure
        // writing any of them takes all of them back rather than leaving the selection half applied.
        await db.SaveChangesAsync(cancellationToken);

        return BatchResultDto.From(outcomes);
    }
}

public record BatchAddMemberNoteCommand(IReadOnlyList<Guid> MemberIds, string Note) : ICommand<BatchResultDto>;

public class BatchAddMemberNoteCommandValidator : AbstractValidator<BatchAddMemberNoteCommand>
{
    public BatchAddMemberNoteCommandValidator()
    {
        RuleFor(x => x.MemberIds).NotEmpty().Must(ids => ids.Count <= BatchLimits.MaxMembersPerBatch)
            .WithMessage($"At most {BatchLimits.MaxMembersPerBatch} members can be changed at once.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>
/// Write the same staff note onto each selected member.
///
/// Writes <see cref="MemberNote"/>, never <c>MedicalNote</c> — bulk-stamping "called about renewal"
/// into a member's medical history would reclassify an operational note as health data, and would
/// put that history in reach of every role holding members.update. See the entity for the full
/// reasoning.
///
/// The author is taken from the JWT rather than the request body, so a note cannot be attributed to
/// somebody else by a caller who edits the payload.
/// </summary>
public class BatchAddMemberNoteCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BatchAddMemberNoteCommand, BatchResultDto>
{
    public async Task<BatchResultDto> Handle(BatchAddMemberNoteCommand request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var requestedIds = request.MemberIds.Distinct().ToList();

        var members = await db.Members
            .Where(m => requestedIds.Contains(m.Id) && accessibleBranchIds.Contains(m.BranchId))
            .ToListAsync(cancellationToken);

        var byId = members.ToDictionary(m => m.Id);
        var outcomes = new List<BatchMemberOutcomeDto>(requestedIds.Count);
        var now = dateTimeProvider.UtcNow;
        var note = request.Note.Trim();

        foreach (var id in requestedIds)
        {
            if (!byId.TryGetValue(id, out var member))
            {
                outcomes.Add(new BatchMemberOutcomeDto(id, null, false, "Member not found."));
                continue;
            }

            db.MemberNotes.Add(new MemberNote
            {
                MemberId = member.Id,
                Note = note,
                AuthorUserId = currentUser.UserId!.Value,
                CreatedAt = now,
            });

            outcomes.Add(new BatchMemberOutcomeDto(id, member.FirstName + " " + member.LastName, true, null));
        }

        await db.SaveChangesAsync(cancellationToken);

        return BatchResultDto.From(outcomes);
    }
}

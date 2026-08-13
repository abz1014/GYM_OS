using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>The workflow's missing "Cancel" step — until this existed, nothing ever set a membership
/// to Cancelled except demo seed data, so the Renewal → Expiry → Reactivation chain had no way in.</summary>
public record CancelMembershipCommand(Guid MemberMembershipId, string? Reason) : ICommand<Unit>;

public class CancelMembershipCommandValidator : AbstractValidator<CancelMembershipCommand>
{
    public CancelMembershipCommandValidator() => RuleFor(x => x.MemberMembershipId).NotEmpty();
}

public class CancelMembershipCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CancelMembershipCommand, Unit>
{
    public async Task<Unit> Handle(CancelMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == request.MemberMembershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), request.MemberMembershipId);

        if (membership.Status is MemberMembershipStatus.Expired or MemberMembershipStatus.Cancelled or MemberMembershipStatus.Transferred)
        {
            throw new ValidationException("Only an active, frozen, or pending membership can be cancelled.");
        }

        /*
         * Cancelling mid-freeze settles the freeze first, exactly as a resume would: credit the days
         * actually spent paused, charge them to FreezeDaysUsed, clear the window.
         *
         * Skipping this made Cancel -> Reactivate a side door around the freeze ledger. The frozen
         * days were never charged to the allowance, and the window rode along untouched — through
         * Cancelled and back out of Reactivate — leaving an ACTIVE membership carrying a live freeze
         * window, which anything that later flips the row through Frozen (the dunning job used to)
         * converts into free EndDate days via Resume.
         */
        if (membership.Status == MemberMembershipStatus.Frozen
            && membership.FreezeStartDate is not null && membership.FreezeEndDate is not null)
        {
            var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
            var creditedDays = MembershipFreezePolicy.CreditableDays(
                membership.FreezeStartDate.Value, membership.FreezeEndDate.Value, today);

            membership.EndDate = membership.EndDate.AddDays(creditedDays);
            membership.FreezeDaysUsed += creditedDays;
            membership.FreezeStartDate = null;
            membership.FreezeEndDate = null;
        }

        membership.Status = MemberMembershipStatus.Cancelled;
        membership.CancellationReason = request.Reason;

        /*
         * Stop chasing the renewal. A Pending dunning attempt left live here kept its own life: the
         * billing job would later charge the card and set the membership back to Active — a cancelled
         * membership revived, and paid for, by a background job. The job now also refuses to collect
         * on a cancelled membership (the belt to this braces), but the intent belongs in the data the
         * moment the human decides it.
         */
        var pendingAttempts = await db.RecurringBillingAttempts
            .Where(a => a.MemberMembershipId == membership.Id && a.Status == RecurringBillingStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var attempt in pendingAttempts)
        {
            attempt.Status = RecurringBillingStatus.Abandoned;
            attempt.LastFailureReason = "Membership was cancelled before the renewal was collected.";
        }

        if (membership.Member is not null)
        {
            var hasOtherLiveMembership = await db.MemberMemberships.AnyAsync(
                mm => mm.MemberId == membership.MemberId && mm.Id != membership.Id
                    && (mm.Status == MemberMembershipStatus.Active
                        || mm.Status == MemberMembershipStatus.Frozen
                        || mm.Status == MemberMembershipStatus.PendingActivation),
                cancellationToken);

            if (!hasOtherLiveMembership)
            {
                membership.Member.Status = MemberStatus.Cancelled;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

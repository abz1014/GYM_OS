using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>Reverses FreezeMembershipCommand — the workflow's missing "resume from freeze" step.</summary>
public record ResumeMembershipCommand(Guid MemberMembershipId) : ICommand<Unit>;

public class ResumeMembershipCommandValidator : AbstractValidator<ResumeMembershipCommand>
{
    public ResumeMembershipCommandValidator() => RuleFor(x => x.MemberMembershipId).NotEmpty();
}

public class ResumeMembershipCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResumeMembershipCommand, Unit>
{
    public async Task<Unit> Handle(ResumeMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == request.MemberMembershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), request.MemberMembershipId);

        if (membership.Status != MemberMembershipStatus.Frozen)
        {
            throw new ValidationException("Only a frozen membership can be resumed.");
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        /*
         * Resuming extends EndDate so that freezing pauses the clock rather than costing the member
         * paid time — standard gym behaviour, and still the intent here. What changed is HOW MUCH.
         *
         * It used to credit the whole requested window and leave the window in place, so the same
         * untouched future dates could be frozen and resumed indefinitely, each round trip adding
         * its full length again. Three cycles of one 30-day window moved a real membership's paid-up
         * date forward by ninety days.
         *
         * Two things close it, and both are needed. The credit is now the time the membership was
         * ACTUALLY paused — a freeze that had not begun yet gives back nothing — and the window is
         * cleared afterwards so it cannot be counted a second time. What was credited is added to
         * FreezeDaysUsed, which is what the plan's allowance is now measured against.
         */
        if (membership.FreezeStartDate is not null && membership.FreezeEndDate is not null)
        {
            var creditedDays = MembershipFreezePolicy.CreditableDays(
                membership.FreezeStartDate.Value, membership.FreezeEndDate.Value, today);

            membership.EndDate = membership.EndDate.AddDays(creditedDays);
            membership.FreezeDaysUsed += creditedDays;

            // Spent. Leaving these set is what allowed the same window to be re-frozen and re-paid.
            membership.FreezeStartDate = null;
            membership.FreezeEndDate = null;
        }

        membership.Status = membership.EndDate < today ? MemberMembershipStatus.Expired : MemberMembershipStatus.Active;

        if (membership.Member is not null && membership.Member.Status == MemberStatus.Frozen)
        {
            membership.Member.Status = membership.Status == MemberMembershipStatus.Expired ? MemberStatus.Expired : MemberStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

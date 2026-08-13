using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

public record FreezeMembershipCommand(Guid MemberMembershipId, DateOnly FreezeStartDate, DateOnly FreezeEndDate) : ICommand<Unit>;

public class FreezeMembershipCommandValidator : AbstractValidator<FreezeMembershipCommand>
{
    public FreezeMembershipCommandValidator()
    {
        RuleFor(x => x.MemberMembershipId).NotEmpty();
        RuleFor(x => x.FreezeEndDate).GreaterThanOrEqualTo(x => x.FreezeStartDate);
    }
}

public class FreezeMembershipCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<FreezeMembershipCommand, Unit>
{
    public async Task<Unit> Handle(FreezeMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships
            .Include(m => m.MembershipPlan)
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == request.MemberMembershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), request.MemberMembershipId);

        /*
         * The entry-state guard this command was missing while every sibling had one.
         *
         * Cancel refuses Expired/Cancelled/Transferred, Resume requires Frozen, Reactivate requires
         * Cancelled — and Freeze accepted anything at all. That made freeze a back door around the
         * whole lifecycle: cancel a membership, freeze it, resume it, and it returns Active with its
         * CancellationReason still attached, having never passed through Reactivate. Confirmed on a
         * live database ("member quit" surviving on an Active row), and it worked on Expired too.
         *
         * Only an Active membership can be paused. Re-freezing an already-frozen one is refused
         * separately because it is a different request wearing the same name — extending a freeze
         * would have to decide what happens to the untaken remainder of the first one.
         */
        if (membership.Status != MemberMembershipStatus.Active)
        {
            throw new ValidationException(membership.Status == MemberMembershipStatus.Frozen
                ? "This membership is already frozen."
                : $"Only an active membership can be frozen; this one is {membership.Status}.");
        }

        // The rule itself lives in MembershipFreezePolicy so the batch endpoint applies exactly the
        // same one — a selection of members spans several plans, and this rule is per plan.
        var maxFreezeDays = membership.MembershipPlan?.MaxFreezeDays ?? MembershipFreezePolicy.NoFreezeAllowance;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var (allowed, reason) = MembershipFreezePolicy.Evaluate(
            maxFreezeDays, membership.FreezeDaysUsed, membership.StartDate, today,
            request.FreezeStartDate, request.FreezeEndDate);

        if (!allowed)
        {
            throw new ValidationException(reason!);
        }

        membership.FreezeStartDate = request.FreezeStartDate;
        membership.FreezeEndDate = request.FreezeEndDate;
        membership.Status = MemberMembershipStatus.Frozen;

        if (membership.Member is not null)
        {
            membership.Member.Status = MemberStatus.Frozen;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

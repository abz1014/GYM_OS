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

public class FreezeMembershipCommandHandler(IApplicationDbContext db) : IRequestHandler<FreezeMembershipCommand, Unit>
{
    public async Task<Unit> Handle(FreezeMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships
            .Include(m => m.MembershipPlan)
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == request.MemberMembershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), request.MemberMembershipId);

        // The rule itself lives in MembershipFreezePolicy so the batch endpoint applies exactly the
        // same one — a selection of members spans several plans, and this rule is per plan.
        var maxFreezeDays = membership.MembershipPlan?.MaxFreezeDays ?? MembershipFreezePolicy.NoFreezeAllowance;
        var (allowed, reason) = MembershipFreezePolicy.Evaluate(maxFreezeDays, request.FreezeStartDate, request.FreezeEndDate);

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

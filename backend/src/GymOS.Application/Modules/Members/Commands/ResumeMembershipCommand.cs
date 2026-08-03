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

public class ResumeMembershipCommandHandler(IApplicationDbContext db) : IRequestHandler<ResumeMembershipCommand, Unit>
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

        // Resuming extends EndDate by the frozen duration — freezing pauses the clock rather than
        // costing the member paid time, matching standard gym membership-freeze behavior.
        if (membership.FreezeStartDate is not null && membership.FreezeEndDate is not null)
        {
            var frozenDays = membership.FreezeEndDate.Value.DayNumber - membership.FreezeStartDate.Value.DayNumber;
            membership.EndDate = membership.EndDate.AddDays(frozenDays);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        membership.Status = membership.EndDate < today ? MemberMembershipStatus.Expired : MemberMembershipStatus.Active;

        if (membership.Member is not null && membership.Member.Status == MemberStatus.Frozen)
        {
            membership.Member.Status = membership.Status == MemberMembershipStatus.Expired ? MemberStatus.Expired : MemberStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

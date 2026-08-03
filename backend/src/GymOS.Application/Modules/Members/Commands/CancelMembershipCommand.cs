using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
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

public class CancelMembershipCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelMembershipCommand, Unit>
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

        membership.Status = MemberMembershipStatus.Cancelled;
        membership.CancellationReason = request.Reason;

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

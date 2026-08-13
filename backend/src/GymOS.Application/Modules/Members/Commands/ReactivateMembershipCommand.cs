using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>Reverses CancelMembershipCommand — the workflow's missing final "Reactivation" step.</summary>
public record ReactivateMembershipCommand(Guid MemberMembershipId) : ICommand<Unit>;

public class ReactivateMembershipCommandValidator : AbstractValidator<ReactivateMembershipCommand>
{
    public ReactivateMembershipCommandValidator() => RuleFor(x => x.MemberMembershipId).NotEmpty();
}

public class ReactivateMembershipCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReactivateMembershipCommand, Unit>
{
    public async Task<Unit> Handle(ReactivateMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == request.MemberMembershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), request.MemberMembershipId);

        if (membership.Status != MemberMembershipStatus.Cancelled)
        {
            throw new ValidationException("Only a cancelled membership can be reactivated.");
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        membership.Status = membership.EndDate < today ? MemberMembershipStatus.Expired : MemberMembershipStatus.Active;
        membership.CancellationReason = null;

        // Cancel now settles any in-flight freeze, so no window should survive to this point — but
        // rows cancelled before that fix, and imported ones, can still carry one. Reactivation must
        // not hand it back as an unspent freeze: it was never charged to the ledger, and an Active
        // row with a live window is exactly the state Resume turns into free membership time.
        membership.FreezeStartDate = null;
        membership.FreezeEndDate = null;

        if (membership.Member is not null && membership.Member.Status == MemberStatus.Cancelled)
        {
            membership.Member.Status = membership.Status == MemberMembershipStatus.Expired ? MemberStatus.Expired : MemberStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

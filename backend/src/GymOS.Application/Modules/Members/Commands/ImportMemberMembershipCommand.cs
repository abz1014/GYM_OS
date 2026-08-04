using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>
/// Records a membership period as a historical fact from a migrated legacy system — deliberately
/// side-effect-free (no invoice, no member-status mutation) unlike RenewMembershipCommand, which is
/// the live signup/renewal workflow. A migrated membership was already billed and paid for in the
/// old system; re-running that workflow here would invoice the member a second time for something
/// they already paid.
/// </summary>
public record ImportMemberMembershipCommand(
    Guid MemberId,
    Guid MembershipPlanId,
    DateOnly StartDate,
    DateOnly EndDate,
    MemberMembershipStatus Status,
    decimal PricePaid,
    string Currency,
    bool AutoRenew) : ICommand<Guid>;

public class ImportMemberMembershipCommandValidator : AbstractValidator<ImportMemberMembershipCommand>
{
    public ImportMemberMembershipCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.MembershipPlanId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PricePaid).GreaterThanOrEqualTo(0);
    }
}

public class ImportMemberMembershipCommandHandler(IApplicationDbContext db) : IRequestHandler<ImportMemberMembershipCommand, Guid>
{
    public async Task<Guid> Handle(ImportMemberMembershipCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Member), request.MemberId);
        }

        var planExists = await db.MembershipPlans.AnyAsync(p => p.Id == request.MembershipPlanId, cancellationToken);
        if (!planExists)
        {
            throw new NotFoundException(nameof(MembershipPlan), request.MembershipPlanId);
        }

        var membership = new MemberMembership
        {
            MemberId = request.MemberId,
            MembershipPlanId = request.MembershipPlanId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
            AutoRenew = request.AutoRenew,
            PricePaid = request.PricePaid,
            Currency = request.Currency
        };

        db.MemberMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        return membership.Id;
    }
}

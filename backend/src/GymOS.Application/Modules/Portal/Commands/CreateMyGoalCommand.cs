using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>A member setting a goal for themselves — owner is resolved from the JWT, never supplied.</summary>
public record CreateMyGoalCommand(string Title, DateOnly? TargetDate) : ICommand<Guid>;

public class CreateMyGoalCommandValidator : AbstractValidator<CreateMyGoalCommand>
{
    public CreateMyGoalCommandValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
}

public class CreateMyGoalCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateMyGoalCommand, Guid>
{
    public async Task<Guid> Handle(CreateMyGoalCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var goal = new MemberGoal
        {
            TenantId = tenantId,
            MemberId = memberId,
            Title = request.Title,
            TargetDate = request.TargetDate,
            CreatedAt = dateTimeProvider.UtcNow
        };

        db.MemberGoals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);

        return goal.Id;
    }
}

/// <summary>
/// Marking one of the member's OWN goals achieved. The ownership check is the security boundary;
/// someone else's goal id answers 404 (not 403) so its existence is never confirmed — the same
/// convention as CancelMyClassBookingCommand.
/// </summary>
public record AchieveMyGoalCommand(Guid GoalId) : ICommand<Unit>;

public class AchieveMyGoalCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AchieveMyGoalCommand, Unit>
{
    public async Task<Unit> Handle(AchieveMyGoalCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var goal = await db.MemberGoals.FirstOrDefaultAsync(g => g.Id == request.GoalId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberGoal), request.GoalId);

        if (goal.MemberId != memberId)
        {
            throw new NotFoundException(nameof(MemberGoal), request.GoalId);
        }

        goal.IsAchieved = true;
        goal.AchievedAt = dateTimeProvider.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

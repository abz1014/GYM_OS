using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// A member choosing how many sessions a week they're training for — the target their home-screen
/// ring closes at. Owner resolved from the JWT, never supplied.
///
/// Upserts: the preference row is created on first change rather than seeded for every member, so
/// "never customised" stays distinguishable from "deliberately set to the default".
/// </summary>
public record SetMyWeeklyGoalCommand(int WeeklySessionGoal) : ICommand<Unit>;

public class SetMyWeeklyGoalCommandValidator : AbstractValidator<SetMyWeeklyGoalCommand>
{
    public SetMyWeeklyGoalCommandValidator() =>
        RuleFor(x => x.WeeklySessionGoal)
            .Must(WeeklyGoalPolicy.IsValidGoal)
            .WithMessage($"Weekly session goal must be between {WeeklyGoalPolicy.MinWeeklySessionGoal} and {WeeklyGoalPolicy.MaxWeeklySessionGoal}.");
}

public class SetMyWeeklyGoalCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetMyWeeklyGoalCommand, Unit>
{
    public async Task<Unit> Handle(SetMyWeeklyGoalCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var preference = await db.MemberTrainingPreferences
            .FirstOrDefaultAsync(p => p.MemberId == memberId, cancellationToken);

        if (preference is null)
        {
            preference = new MemberTrainingPreference { TenantId = tenantId, MemberId = memberId };
            db.MemberTrainingPreferences.Add(preference);
        }

        preference.WeeklySessionGoal = request.WeeklySessionGoal;
        preference.UpdatedAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

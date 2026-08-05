using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Experience;
using MediatR;

namespace GymOS.Application.Modules.Challenges.Commands;

/// <summary>Staff creates a community challenge — tenant-wide when BranchId is null, restricted to
/// one branch otherwise.</summary>
public record CreateChallengeCommand(
    string Name, string? Description, Guid? BranchId, DateOnly StartDate, DateOnly EndDate, int TargetWorkoutCount) : ICommand<Guid>;

public class CreateChallengeCommandValidator : AbstractValidator<CreateChallengeCommand>
{
    public CreateChallengeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TargetWorkoutCount).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public class CreateChallengeCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateChallengeCommand, Guid>
{
    public async Task<Guid> Handle(CreateChallengeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var challenge = new CommunityChallenge
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TargetWorkoutCount = request.TargetWorkoutCount,
            CreatedByUserId = currentUser.UserId
        };

        db.CommunityChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        return challenge.Id;
    }
}

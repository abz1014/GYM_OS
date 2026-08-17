using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// Ends someone's access without deleting them — their audit trail, the payments they took and the
/// classes they taught all stay attached to a real person.
///
/// Two deactivations are refused, for the same reason rather than two: LoginCommand rejects inactive
/// users, so an account switched off here can only be switched back on from this same screen. Locking
/// out the last person who can reach the screen is therefore unrecoverable inside the product.
/// </summary>
public record DeactivateStaffCommand(Guid Id) : ICommand<Unit>;

public class DeactivateStaffCommandValidator : AbstractValidator<DeactivateStaffCommand>
{
    public DeactivateStaffCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class DeactivateStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeactivateStaffCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateStaffCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        // Checked before the last-manager rule so the owner of a one-person gym gets the message that
        // actually describes what they just tried to do.
        if (currentUser.UserId == user.Id)
        {
            throw new ValidationException("You cannot deactivate your own account.");
        }

        if (await StaffAccountGuards.IsLastActiveStaffManagerAsync(db, tenantId, user.Id, cancellationToken))
        {
            throw new ValidationException(
                "This is the last active account that can manage staff. Give another account a role that can manage staff first.");
        }

        user.IsActive = false;

        // Sessions in flight die on their own: RefreshTokenCommand re-checks User.IsActive on every
        // rotation, so the outstanding access token expires and nothing renews it.
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

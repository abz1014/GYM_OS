using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// Puts a deactivated account back to work. The password is untouched — someone returning from leave
/// signs in with what they had; someone who has forgotten it gets reset-password, which is a separate
/// deliberate act rather than a side effect of being re-hired.
/// </summary>
public record ReactivateStaffCommand(Guid Id) : ICommand<Unit>;

public class ReactivateStaffCommandValidator : AbstractValidator<ReactivateStaffCommand>
{
    public ReactivateStaffCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ReactivateStaffCommandHandler(IApplicationDbContext db) : IRequestHandler<ReactivateStaffCommand, Unit>
{
    public async Task<Unit> Handle(ReactivateStaffCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        user.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

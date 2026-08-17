using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// Edits a staff account: their name, their phone, the job they do and the sites they can work at.
///
/// Email is absent on purpose. It is the username LoginCommand resolves an account by, so changing it
/// here would silently invalidate the credentials the person already has, with no path back for
/// anyone but the developer who can run SQL. Someone who needs a different address gets a new account.
/// </summary>
public record UpdateStaffCommand(
    Guid Id, string FirstName, string LastName, string? Phone, string RoleName, IReadOnlyList<Guid> BranchIds)
    : ICommand<Unit>;

public class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.RoleName).NotEmpty();
        RuleFor(x => x.BranchIds).NotEmpty().WithMessage("A staff member must have access to at least one branch.");
    }
}

public class UpdateStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateStaffCommand, Unit>
{
    public async Task<Unit> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        // Loaded through the tenant-filtered DbSet, so another gym's user is not "forbidden" — it does
        // not exist as far as this caller is concerned, and answering 404 rather than 403 is what
        // stops this endpoint being a probe for which user ids are real elsewhere.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        var role = await StaffAccountGuards.ResolveAssignableRoleAsync(db, tenantId, request.RoleName, cancellationToken);
        var branchIds = await StaffAccountGuards.ResolveBranchIdsAsync(db, request.BranchIds, cancellationToken);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Phone = request.Phone;

        // Role and branch access are replaced wholesale rather than diffed: the screen sends the
        // complete intended set, and rebuilding it is the only version that can also REMOVE access.
        var existingRoles = await db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(cancellationToken);
        db.UserRoles.RemoveRange(existingRoles);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        var existingAccess = await db.UserBranchAccesses.Where(uba => uba.UserId == user.Id).ToListAsync(cancellationToken);
        db.UserBranchAccesses.RemoveRange(existingAccess);
        foreach (var branchId in branchIds)
        {
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

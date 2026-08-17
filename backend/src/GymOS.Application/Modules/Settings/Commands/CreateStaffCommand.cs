using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// Hires someone. Until this existed the only creatable login was a trainer, and every other staff
/// account in the product came out of the demo seeder — hiring a receptionist meant a developer with
/// a CLI, which is not a thing a gym has.
/// </summary>
public record CreateStaffCommand(
    string Email, string FirstName, string LastName, string? Phone, string RoleName, IReadOnlyList<Guid> BranchIds)
    : ICommand<CreateStaffResultDto>;

public class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.RoleName).NotEmpty();
        // An account with no branch can see nothing: every branch-scoped query filters on the user's
        // access list, so a branchless staff member logs in to a product with no data in it.
        RuleFor(x => x.BranchIds).NotEmpty().WithMessage("A staff member must have access to at least one branch.");
    }
}

public class CreateStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateStaffCommand, CreateStaffResultDto>
{
    public async Task<CreateStaffResultDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var role = await StaffAccountGuards.ResolveAssignableRoleAsync(db, tenantId, request.RoleName, cancellationToken);
        var branchIds = await StaffAccountGuards.ResolveBranchIdsAsync(db, request.BranchIds, cancellationToken);

        /*
         * Uniqueness is checked across ALL tenants, deliberately not through the tenant-filtered set.
         *
         * Email is the username: LoginCommand looks it up with IgnoreQueryFilters() precisely because
         * a person signing in has no tenant yet. A tenant-scoped uniqueness check would therefore let
         * two gyms both hold an account on the same address, and the second one would be created
         * successfully and then be unable to sign in — a login that fails with a correct password and
         * no visible cause.
         */
        var emailTaken = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailTaken)
        {
            throw new ValidationException($"Email \"{request.Email}\" is already in use.");
        }

        var temporaryPassword = TokenHasher.GenerateTemporaryPassword();

        var user = new User
        {
            TenantId = tenantId,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(temporaryPassword),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            IsActive = true
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        foreach (var branchId in branchIds)
        {
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CreateStaffResultDto(user.Id, temporaryPassword);
    }
}

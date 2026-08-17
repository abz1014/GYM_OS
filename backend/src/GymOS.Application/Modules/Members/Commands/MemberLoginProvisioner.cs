using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Identity;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>
/// Creates the login a Member record needs to reach the self-service portal — a User in the tenant's
/// Member role, scoped to exactly the member's own branch, with a temporary password only the caller
/// of this method ever sees in plaintext. Shared between CreateMemberCommand (a new member gets one
/// immediately) and ProvisionMemberLoginCommand (an existing loginless member gets one retroactively)
/// because they are the same act of provisioning happening at two different moments.
/// </summary>
internal static class MemberLoginProvisioner
{
    /// <summary>
    /// Email uniqueness is checked GLOBALLY (IgnoreQueryFilters), not tenant-scoped — the same reason
    /// CreateStaffCommand checks it globally. LoginCommand resolves an email with no tenant context at
    /// all at sign-in time, so a tenant-scoped uniqueness check would let two different gyms create an
    /// account on the same address and strand the second one with a login that can never resolve.
    /// </summary>
    public static async Task<(User User, string TemporaryPassword)> ProvisionAsync(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        Guid tenantId,
        Guid branchId,
        string email,
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var emailTaken = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken);
        if (emailTaken)
        {
            throw new ValidationException($"Email \"{email}\" is already in use.");
        }

        var memberRole = await db.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == RoleNames.Member, cancellationToken)
            ?? throw new InvalidOperationException("The Member role is missing for this tenant.");

        var temporaryPassword = TokenHasher.GenerateTemporaryPassword();

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHasher.Hash(temporaryPassword),
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            IsActive = true
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = memberRole.Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchId });

        return (user, temporaryPassword);
    }
}

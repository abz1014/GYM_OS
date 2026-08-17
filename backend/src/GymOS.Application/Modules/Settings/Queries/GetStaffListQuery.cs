using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

/// <summary>
/// The roster behind the staff screen: everyone in the tenant who works here, plus the roles they can
/// be given.
///
/// "Staff" is defined by exclusion — every user in the tenant EXCEPT those holding the Member role.
/// That direction matters. A gym has a handful of employees and thousands of members sharing one
/// Users table, so listing "users with a staff role" and listing "users that are not members" only
/// look equivalent until someone adds a role; the first quietly drops the new role's holders off the
/// screen that is supposed to manage them, the second cannot.
/// </summary>
public record GetStaffListQuery : IQuery<StaffListDto>;

public class GetStaffListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetStaffListQuery, StaffListDto>
{
    public async Task<StaffListDto> Handle(GetStaffListQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        // db.Roles/db.Users are already tenant-filtered by the global query filter; the explicit
        // TenantId predicate here is belt-and-braces on the one screen that hands out logins.
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        var memberRoleIds = roles.Where(r => r.Name == RoleNames.Member).Select(r => r.Id).ToHashSet();
        var roleNamesById = roles.ToDictionary(r => r.Id, r => r.Name);

        var users = await db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.Phone, u.IsActive, u.LastLoginAt })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var roleAssignments = await db.UserRoles.AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync(cancellationToken);

        var branchAccess = await db.UserBranchAccesses.AsNoTracking()
            .Where(uba => userIds.Contains(uba.UserId))
            .Select(uba => new { uba.UserId, uba.BranchId })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleAssignments
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleId).ToList());

        var branchesByUser = branchAccess
            .GroupBy(uba => uba.UserId)
            .ToDictionary(g => g.Key, g => g.Select(uba => uba.BranchId).Distinct().ToList());

        var staff = users
            .Where(u => !rolesByUser.GetValueOrDefault(u.Id, []).Any(memberRoleIds.Contains))
            .Select(u => new StaffMemberDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Phone,
                u.IsActive,
                // The seeder gives every user exactly one role, and this screen writes exactly one.
                // Alphabetical-first is a display fallback for data that got there another way, not a
                // policy — picking one beats throwing on the only screen that can fix the account.
                rolesByUser.GetValueOrDefault(u.Id, [])
                    .Select(roleId => roleNamesById.GetValueOrDefault(roleId, string.Empty))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .FirstOrDefault() ?? string.Empty,
                branchesByUser.GetValueOrDefault(u.Id, []),
                // Read, never ordered or compared, on purpose: LastLoginAt is a DateTimeOffset and
                // SQLite (the test provider) translates neither operation. The sort below is on names.
                u.LastLoginAt))
            .OrderBy(s => s.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assignableRoles = roles
            .Where(r => !memberRoleIds.Contains(r.Id))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new RoleDto(r.Id, r.Name))
            .ToList();

        return new StaffListDto(staff, assignableRoles);
    }
}

using GymOS.Application.Modules.Settings.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Settings;

/// <summary>
/// SetRolePermissionCommand's core business rule: granting/revoking must be idempotent. The
/// permission-matrix editor toggles a checkbox and re-sends the same command on every click; without
/// idempotency, double-granting would create duplicate RolePermission rows and double-revoking would
/// throw on the second click instead of silently no-op'ing.
/// </summary>
public class SetRolePermissionCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Granting_a_permission_twice_does_not_create_a_duplicate_row()
    {
        var (tenantId, roleId, permissionId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        await SendAsync(new SetRolePermissionCommand(roleId, permissionId, Granted: true));
        await SendAsync(new SetRolePermissionCommand(roleId, permissionId, Granted: true));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var count = await db.RolePermissions.CountAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Revoking_a_permission_that_was_never_granted_does_not_throw()
    {
        var (tenantId, roleId, permissionId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        await SendAsync(new SetRolePermissionCommand(roleId, permissionId, Granted: false));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Granting_then_revoking_leaves_no_row_behind()
    {
        var (tenantId, roleId, permissionId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        await SendAsync(new SetRolePermissionCommand(roleId, permissionId, Granted: true));
        await SendAsync(new SetRolePermissionCommand(roleId, permissionId, Granted: false));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId)).ShouldBeFalse();
    }

    private async Task<(Guid TenantId, Guid RoleId, Guid PermissionId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var role = new Role { TenantId = tenant.Id, Name = $"Role-{Guid.NewGuid():N}" };
        db.Roles.Add(role);

        var permission = new Permission { Code = $"test.permission.{Guid.NewGuid():N}", Module = "test", Description = "Test permission" };
        db.Permissions.Add(permission);

        await db.SaveChangesAsync();
        return (tenant.Id, role.Id, permission.Id);
    }
}

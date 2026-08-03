using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Identity;
using GymOS.Infrastructure.Persistence;

namespace GymOS.Api.IntegrationTests.TestSupport;

/// <summary>Seeds directly via GymOsDbContext rather than going through the API's own commands —
/// keeps each test's fixture data independent of whatever the Auth module happens to require.</summary>
public static class TestDataSeeder
{
    public const string Password = "Correct@12345";

    public static async Task<(Guid TenantId, Guid UserId, string Email)> SeedUserWithPermissionsAsync(
        GymOsDbContext db, params string[] permissionCodes)
    {
        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = new PasswordHasher().Hash(Password),
            FirstName = "Test",
            LastName = "User",
            IsActive = true
        };
        db.Users.Add(user);

        var role = new Role { TenantId = tenant.Id, Name = $"Role-{Guid.NewGuid():N}" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        foreach (var code in permissionCodes)
        {
            // Permissions are a global catalog seeded once by DemoDataSeeder in the real app;
            // this test DB starts empty, so each permission code is created on first use.
            var permission = new Permission { Code = code, Module = code.Split('.')[0], Description = code };
            db.Permissions.Add(permission);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, email);
    }
}

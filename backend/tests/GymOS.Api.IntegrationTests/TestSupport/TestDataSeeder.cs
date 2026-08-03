using GymOS.Domain.Attendance;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Identity;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
            // Permissions are a global catalog seeded once by DemoDataSeeder in the real app, and
            // Permission.Code is unique — a test class that seeds the same code across more than
            // one test method (same shared DB per IClassFixture<GymOsWebApplicationFactory>) must
            // reuse the existing row rather than re-inserting it.
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code)
                ?? new Permission { Code = code, Module = code.Split('.')[0], Description = code };

            if (db.Entry(permission).State == EntityState.Detached)
            {
                db.Permissions.Add(permission);
            }

            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, email);
    }

    /// <summary>A user holding only Portal.View, linked (via Member.UserId) to a real Member row
    /// with one attendance record — the exact shape needed to exercise the member self-service
    /// portal and prove it never leaks another member's data.</summary>
    public static async Task<(Guid TenantId, Guid UserId, string Email, Guid MemberId, string MemberFullName)> SeedPortalMemberAsync(
        GymOsDbContext db)
    {
        var (tenantId, userId, email) = await SeedUserWithPermissionsAsync(db, "portal.view");

        // Branch is tenant-scoped; this call has no ambient JWT/tenant context, so the global
        // query filter would otherwise silently match zero rows here (same reason DemoDataSeeder
        // and the background jobs use IgnoreQueryFilters() everywhere they run outside a request).
        var branchId = await db.Branches.IgnoreQueryFilters().Where(b => b.TenantId == tenantId).Select(b => b.Id).FirstAsync();
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = userId, BranchId = branchId });

        var member = new Member
        {
            TenantId = tenantId,
            BranchId = branchId,
            UserId = userId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Portal",
            LastName = $"Member-{Guid.NewGuid():N}"[..8],
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberId = member.Id,
            CheckInAt = DateTimeOffset.UtcNow.AddDays(-1),
            Method = AttendanceMethod.Manual
        });

        await db.SaveChangesAsync();
        return (tenantId, userId, email, member.Id, $"{member.FirstName} {member.LastName}");
    }
}

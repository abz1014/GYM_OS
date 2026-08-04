using Bogus;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Attendance;
using GymOS.Domain.Billing;
using GymOS.Domain.Crm;
using GymOS.Domain.Equipment;
using GymOS.Domain.Identity;
using GymOS.Domain.Inventory;
using GymOS.Domain.Maintenance;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Notifications;
using GymOS.Domain.Settings;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.Seeding;

/// <summary>
/// Populates a fresh database with a realistic, self-consistent demo dataset matching the spec's
/// required volumes. Every date is computed relative to DateTimeOffset.UtcNow at seed time (never
/// hardcoded) so "expiring this week"/"overdue maintenance"/"today's revenue" dashboard widgets
/// are always populated no matter when the demo actually runs. Randomizer.Seed is fixed so a
/// fresh clone + reseed produces the same dataset every time. Idempotent: does nothing if a
/// Tenant already exists.
/// </summary>
public partial class DemoDataSeeder(GymOsDbContext db, IPasswordHasher passwordHasher, ILogger<DemoDataSeeder> logger)
{
    private const string DemoPassword = "Demo@12345";
    private readonly Faker _faker = new();

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo data already present — skipping seed.");
            return;
        }

        Randomizer.Seed = new Random(20260803);

        // Wrapped in one transaction so a failure partway through (e.g. a bug in a later step)
        // can't leave a half-seeded tenant behind that then fools the AnyAsync check above on retry.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var tenant = await SeedTenantAndBranchesAsync(cancellationToken);
        // Order by name so branches[0] is deterministic and matches the frontend's default branch
        // (GetBranchesQuery also orders by name → "Downtown" first). Without this, a raw load can
        // return branches in any order, landing all the single-branch demo data (classes, trainers,
        // equipment, …) on a branch the UI doesn't select by default — making the demo look empty.
        var branches = await db.Branches.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenant.Id).OrderBy(b => b.Name).ToListAsync(cancellationToken);

        var (roles, permissions) = await SeedRolesAndPermissionsAsync(tenant.Id, cancellationToken);
        var demoUsers = await SeedDemoUsersAsync(tenant.Id, branches, roles, cancellationToken);

        var plans = await SeedMembershipPlansAsync(tenant.Id, cancellationToken);
        var members = await SeedMembersAsync(tenant.Id, branches, plans, cancellationToken);

        await SeedAttendanceAsync(tenant.Id, branches, members, cancellationToken);
        await SeedInvoicesAndPaymentsAsync(tenant.Id, branches, members, demoUsers, cancellationToken);
        // branches[0] is the branch group classes are seeded into (see SeedClassesAsync), so link
        // the demo member there — it lets the Step 3 member-booking demo work out of the box.
        await LinkDemoMemberAccountAsync(demoUsers, branches[0].Id, cancellationToken);

        var trainers = await SeedTrainersAsync(tenant.Id, branches, demoUsers, cancellationToken);
        await SeedTrainerAssignmentsAsync(trainers, members, cancellationToken);
        await SeedClassesAsync(tenant.Id, branches, trainers, members, cancellationToken);

        var assets = await SeedEquipmentAsync(tenant.Id, branches, cancellationToken);
        await SeedMaintenanceAsync(tenant.Id, branches, assets, demoUsers, cancellationToken);
        await SeedInventoryAsync(tenant.Id, branches, cancellationToken);
        await SeedLeadsAsync(tenant.Id, branches, demoUsers, cancellationToken);
        await SeedNotificationTemplatesAsync(tenant.Id, cancellationToken);
        await SeedExerciseLibraryAsync(tenant.Id, cancellationToken);
        await SeedFoodLibraryAsync(tenant.Id, cancellationToken);
        // Must run after both libraries above — it looks up exercises/food items by name.
        await SeedDemoMemberIntelligenceDataAsync(demoUsers, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Demo data seed complete for tenant {TenantId}.", tenant.Id);
    }

    private async Task<Tenant> SeedTenantAndBranchesAsync(CancellationToken cancellationToken)
    {
        var tenant = new Tenant { Name = "Titan Fitness", Slug = "titan-fitness", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Tenants.Add(tenant);

        var branchNames = new[] { "Downtown", "Uptown", "Westside" };
        foreach (var name in branchNames)
        {
            db.Branches.Add(new Branch
            {
                TenantId = tenant.Id,
                Name = $"Titan Fitness - {name}",
                AddressLine = _faker.Address.StreetAddress(),
                City = _faker.Address.City(),
                Country = "United States",
                TimeZone = "America/New_York",
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        db.GymProfiles.Add(new GymProfile
        {
            TenantId = tenant.Id,
            LegalName = "Titan Fitness LLC",
            DisplayName = "Titan Fitness",
            SupportEmail = "support@titanfitness.demo",
            SupportPhone = "+1-555-0100",
            DefaultCurrency = "USD",
            DefaultTimeZone = "America/New_York"
        });

        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private async Task<(Dictionary<string, Role> Roles, Dictionary<string, Permission> Permissions)> SeedRolesAndPermissionsAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = RoleNames.All.ToDictionary(name => name, name => new Role { TenantId = tenantId, Name = name, IsSystemRole = true });
        db.Roles.AddRange(roles.Values);

        var permissionDefinitions = GetPermissionCatalog();
        var permissions = permissionDefinitions.ToDictionary(p => p.Code, p => new Permission { Code = p.Code, Module = p.Module, Description = p.Description });
        db.Permissions.AddRange(permissions.Values);

        await db.SaveChangesAsync(cancellationToken);

        var rolePermissionMap = new Dictionary<string, string[]>
        {
            // Portal.View is the member self-service grant, not a staff capability — its own catalog
            // entry reads "view OWN member profile". Owner/Manager are staff and are never linked to
            // a Member row, so handing it to them (as a side effect of "all permissions") only put
            // three dead sidebar links — My Account / My Classes / My Progress — that can render
            // nothing but "ask the front desk to link your account". Excluded from every staff role;
            // the Member role below is the only one that gets it.
            [RoleNames.Owner] = permissions.Keys.Where(c => c != PermissionCodes.Portal.View).ToArray(),
            [RoleNames.Manager] = permissions.Keys
                .Where(c => c != PermissionCodes.Settings.ManagePermissions && c != PermissionCodes.Portal.View)
                .ToArray(),
            [RoleNames.Receptionist] =
            [
                PermissionCodes.Dashboard.View, PermissionCodes.Members.View, PermissionCodes.Members.Create, PermissionCodes.Members.Update,
                PermissionCodes.Members.ManageMembership, PermissionCodes.Memberships.View, PermissionCodes.Billing.View,
                PermissionCodes.Billing.CreateInvoice, PermissionCodes.Billing.RecordPayment, PermissionCodes.Attendance.View,
                PermissionCodes.Attendance.CheckIn, PermissionCodes.Crm.View, PermissionCodes.Crm.ManageLeads,
                PermissionCodes.Classes.View, PermissionCodes.Classes.Manage
            ],
            [RoleNames.Trainer] =
            [
                PermissionCodes.Dashboard.View, PermissionCodes.Members.View, PermissionCodes.Trainers.View,
                PermissionCodes.Attendance.View, PermissionCodes.Workouts.View, PermissionCodes.Workouts.Manage,
                PermissionCodes.Classes.View
            ],
            [RoleNames.Nutritionist] =
            [
                PermissionCodes.Dashboard.View, PermissionCodes.Members.View, PermissionCodes.Nutrition.View, PermissionCodes.Nutrition.Manage
            ],
            [RoleNames.Accountant] =
            [
                PermissionCodes.Dashboard.View, PermissionCodes.Billing.View, PermissionCodes.Billing.CreateInvoice,
                PermissionCodes.Billing.RecordPayment, PermissionCodes.Billing.IssueRefund, PermissionCodes.Memberships.View,
                PermissionCodes.Reports.View
            ],
            [RoleNames.Maintenance] =
            [
                PermissionCodes.Dashboard.View, PermissionCodes.Maintenance.View, PermissionCodes.Maintenance.Manage, PermissionCodes.Equipment.View
            ],
            // Deliberately NOT Attendance/Workouts/Nutrition/Dashboard.View — those grant staff-wide
            // access to every member's records and every member's business figures. A gym member
            // gets Portal.View instead, which only ever resolves to their own data server-side.
            [RoleNames.Member] = [PermissionCodes.Portal.View]
        };

        foreach (var (roleName, codes) in rolePermissionMap)
        {
            var role = roles[roleName];
            foreach (var code in codes)
            {
                if (permissions.TryGetValue(code, out var permission))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (roles, permissions);
    }

    private static List<(string Code, string Module, string Description)> GetPermissionCatalog() =>
    [
        (PermissionCodes.Members.View, "members", "View members"),
        (PermissionCodes.Members.Create, "members", "Register new members"),
        (PermissionCodes.Members.Update, "members", "Edit member profiles"),
        (PermissionCodes.Members.Delete, "members", "Delete members"),
        (PermissionCodes.Members.ManageMembership, "members", "Freeze/renew/transfer memberships"),
        (PermissionCodes.Memberships.View, "memberships", "View membership plans"),
        (PermissionCodes.Memberships.ManagePlans, "memberships", "Create/edit membership plans"),
        (PermissionCodes.Memberships.ManageDiscounts, "memberships", "Create/edit discounts and coupons"),
        (PermissionCodes.Billing.View, "billing", "View invoices and payments"),
        (PermissionCodes.Billing.CreateInvoice, "billing", "Create invoices"),
        (PermissionCodes.Billing.RecordPayment, "billing", "Record payments"),
        (PermissionCodes.Billing.IssueRefund, "billing", "Issue refunds"),
        (PermissionCodes.Attendance.View, "attendance", "View attendance history"),
        (PermissionCodes.Attendance.CheckIn, "attendance", "Check members in/out"),
        (PermissionCodes.Dashboard.View, "dashboard", "View executive dashboard"),
        (PermissionCodes.Portal.View, "portal", "View own member profile, attendance, workouts, and nutrition"),
        (PermissionCodes.Settings.View, "settings", "View gym settings"),
        (PermissionCodes.Settings.ManageBranches, "settings", "Manage branches"),
        (PermissionCodes.Settings.ManagePermissions, "settings", "Manage the role permission matrix"),
        (PermissionCodes.Settings.ManageGymProfile, "settings", "Manage gym profile"),
        (PermissionCodes.Crm.View, "crm", "View CRM leads"),
        (PermissionCodes.Crm.ManageLeads, "crm", "Manage CRM leads"),
        (PermissionCodes.Trainers.View, "trainers", "View trainers"),
        (PermissionCodes.Trainers.Manage, "trainers", "Manage trainers"),
        (PermissionCodes.Classes.View, "classes", "View class schedule and sessions"),
        (PermissionCodes.Classes.Manage, "classes", "Manage class types, schedules, and sessions"),
        (PermissionCodes.Equipment.View, "equipment", "View equipment assets"),
        (PermissionCodes.Equipment.Manage, "equipment", "Manage equipment assets"),
        (PermissionCodes.Maintenance.View, "maintenance", "View maintenance work orders"),
        (PermissionCodes.Maintenance.Manage, "maintenance", "Manage maintenance work orders"),
        (PermissionCodes.Inventory.View, "inventory", "View inventory"),
        (PermissionCodes.Inventory.Manage, "inventory", "Manage inventory"),
        (PermissionCodes.Workouts.View, "workouts", "View workouts"),
        (PermissionCodes.Workouts.Manage, "workouts", "Manage workouts"),
        (PermissionCodes.Nutrition.View, "nutrition", "View nutrition plans"),
        (PermissionCodes.Nutrition.Manage, "nutrition", "Manage nutrition plans"),
        (PermissionCodes.Reports.View, "reports", "View reports"),
        (PermissionCodes.Migration.Manage, "migration", "Run data migration imports"),
        (PermissionCodes.Notifications.View, "notifications", "View notification center"),
        (PermissionCodes.Notifications.Manage, "notifications", "Manage notification templates and trigger checks")
    ];

    private async Task<Dictionary<string, User>> SeedDemoUsersAsync(
        Guid tenantId, List<Branch> branches, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, User>();
        var passwordHash = passwordHasher.Hash(DemoPassword);

        foreach (var roleName in RoleNames.All)
        {
            var user = new User
            {
                TenantId = tenantId,
                Email = $"{roleName.ToLowerInvariant()}@titanfitness.demo",
                PasswordHash = passwordHash,
                FirstName = roleName,
                LastName = "Demo",
                IsActive = true
            };

            db.Users.Add(user);
            users[roleName] = user;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (roleName, user) in users)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[roleName].Id });

            var accessibleBranches = roleName is RoleNames.Owner or RoleNames.Manager ? branches : [branches[0]];
            foreach (var branch in accessibleBranches)
            {
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return users;
    }

    private async Task<List<MembershipPlan>> SeedMembershipPlansAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var plans = new List<MembershipPlan>
        {
            new() { TenantId = tenantId, Name = "Monthly Basic", Type = MembershipPlanType.Monthly, DurationDays = 30, Price = 49.99m, Currency = "USD", MaxFreezeDays = 0, IsActive = true },
            new() { TenantId = tenantId, Name = "Quarterly Standard", Type = MembershipPlanType.Quarterly, DurationDays = 90, Price = 129.99m, Currency = "USD", MaxFreezeDays = 7, IsActive = true },
            new() { TenantId = tenantId, Name = "Annual Gold", Type = MembershipPlanType.Annual, DurationDays = 365, Price = 449.99m, Currency = "USD", MaxFreezeDays = 30, IsActive = true },
            new() { TenantId = tenantId, Name = "Family Plan", Type = MembershipPlanType.Family, DurationDays = 365, Price = 799.99m, Currency = "USD", MaxFreezeDays = 30, IsActive = true },
            new() { TenantId = tenantId, Name = "Corporate Plan", Type = MembershipPlanType.Corporate, DurationDays = 365, Price = 399.99m, Currency = "USD", MaxFreezeDays = 15, IsActive = true },
            new() { TenantId = tenantId, Name = "Student Flex", Type = MembershipPlanType.Custom, DurationDays = 180, Price = 199.99m, Currency = "USD", MaxFreezeDays = 0, IsActive = true }
        };

        db.MembershipPlans.AddRange(plans);
        await db.SaveChangesAsync(cancellationToken);
        return plans;
    }
}

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

    /// <summary>Dictionary key for the second Member-role login (member2@) — a coached member on a
    /// trainer's written programme, so both of Step 1's logging tiers are visible at once. Not a role
    /// name; it maps to <see cref="RoleNames.Member"/> like the first one does.</summary>
    private const string CoachedMemberKey = "CoachedMember";
    private readonly Faker _faker = new();

    public async Task SeedAsync(SeedProfile profile = SeedProfile.Demo, CancellationToken cancellationToken = default)
    {
        /*
         * The permission catalogue is synced BEFORE the "already seeded" bail-out, and that ordering
         * is the whole point.
         *
         * Permission ROWS are not reflection-generated — they come from GetPermissionCatalog() below,
         * and they are only ever written on a first-time seed. Every deployed database is non-empty,
         * so the seeder returns here and a permission code added in a release simply never exists in
         * production: its authorization policy resolves, finds no row, and every endpoint behind it
         * answers 403 for everybody. Caught exactly that way on this database — settings.manage_staff
         * shipped with a working policy, no row, and a Staff screen that 403'd for the Owner.
         *
         * Retired codes have the mirror problem: members.delete was deleted from the catalogue and
         * kept its row, so the permission matrix went on offering a switch that grants nothing.
         */
        await SyncPermissionCatalogAsync(cancellationToken);
        await BackfillOrphanedTrainerAssignmentsAsync(cancellationToken);

        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo data already present — skipping seed.");
            return;
        }

        logger.LogInformation("Seeding with the {Profile} profile.", profile);

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
        var members = await SeedMembersAsync(tenant.Id, branches, plans, profile, cancellationToken);

        await SeedAttendanceAsync(tenant.Id, branches, members, cancellationToken);
        await SeedInvoicesAndPaymentsAsync(tenant.Id, branches, members, demoUsers, cancellationToken);
        /*
         * branches[0] is the branch group classes are seeded into (see SeedClassesAsync), so link
         * the demo member there — it lets the Step 3 member-booking demo work out of the box.
         *
         * On the pilot profile the first two members are handed over by name instead of being picked
         * by eligibility. Every pilot member already has a login, so an eligibility search would
         * relink one that already belongs to someone and strand the account it displaced.
         */
        var reserved = profile == SeedProfile.Pilot ? members.Take(2).ToList() : null;
        await LinkDemoMemberAccountAsync(demoUsers, branches[0].Id, reserved, cancellationToken);

        var trainers = await SeedTrainersAsync(tenant.Id, branches, demoUsers, profile, cancellationToken);
        await SeedTrainerAssignmentsAsync(trainers, members, cancellationToken);
        await SeedClassesAsync(tenant.Id, branches, trainers, members, cancellationToken);

        var assets = await SeedEquipmentAsync(tenant.Id, branches, cancellationToken);
        await SeedMaintenanceAsync(tenant.Id, branches, assets, demoUsers, cancellationToken);
        await SeedInventoryAsync(tenant.Id, branches, cancellationToken);
        await SeedLeadsAsync(tenant.Id, branches, demoUsers, cancellationToken);
        await SeedNotificationTemplatesAsync(tenant.Id, cancellationToken);
        await SeedExerciseLibraryAsync(tenant.Id, cancellationToken);
        await SeedFoodLibraryAsync(tenant.Id, cancellationToken);
        // Must run after the exercise library — its skill trees reference exercises by name.
        await SeedSkillTreesAsync(tenant.Id, cancellationToken);
        // Must run after the exercise library (programmes are built from it), after trainer pairings
        // (only coached members get a plan) and after the demo-member links (both member logins are
        // pinned to a known logging tier at the end of it).
        await SeedWorkoutPlansAsync(tenant.Id, trainers, cancellationToken);
        // Must run after the exercise library — every session it generates picks real exercises, and
        // it is what gives the member-facing engine (streaks, mastery, records, leaderboards) any
        // data at all beyond the single demo account.
        var curatedMember = await db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == demoUsers[RoleNames.Member].Id, cancellationToken);
        await SeedMemberActivityAsync(tenant.Id, members, curatedMember?.Id, cancellationToken);
        // Must run after both libraries above — it looks up exercises/food items by name.
        await SeedDemoMemberIntelligenceDataAsync(demoUsers, cancellationToken);
        // Must run after the food library — gives the Nutritionist login a real roster to open on,
        // which is derived from plan authorship and would otherwise be empty for every user.
        await SeedNutritionCaseloadAsync(tenant.Id, members, demoUsers, cancellationToken);
        // Must run after the intelligence data above — its "already in progress" participant counts
        // the demo member's just-seeded workout history.
        await SeedCommunityChallengesAsync(demoUsers, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Demo data seed complete for tenant {TenantId}.", tenant.Id);
    }

    private async Task<Tenant> SeedTenantAndBranchesAsync(CancellationToken cancellationToken)
    {
        var tenant = new Tenant { Name = "Titan Fitness", Slug = "titan-fitness", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Tenants.Add(tenant);

        // Capacity is a number a gym looks up off its licence, so the sites differ — and Westside is
        // deliberately left unset. Every surface that divides by capacity has to hold up for a gym
        // that never filled it in, and a demo where all three are populated would never show that
        // path to anyone.
        var branches = new[]
        {
            (Name: "Downtown", Capacity: (int?)180),
            (Name: "Uptown", Capacity: (int?)120),
            (Name: "Westside", Capacity: (int?)null),
        };
        foreach (var (name, capacity) in branches)
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
                Capacity = capacity,
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

        var rolePermissionMap = GetRolePermissionMap(permissions.Keys);

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
        (PermissionCodes.Settings.ManageStaff, "settings", "Create, edit, and deactivate staff accounts"),
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
        (PermissionCodes.Notifications.Manage, "notifications", "Manage notification templates and trigger checks"),
        (PermissionCodes.Experience.Manage, "experience", "Create community challenges")
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

        // A second member, because the home screen can only ever show one member's situation and there
        // are two worth showing. Step 1 proposes a session from a trainer's plan when there is one and
        // from the member's own last session otherwise, and the second case is the majority — only
        // 12.5-29% of gym members work with a trainer at all. With a single login the demo has to pick
        // one of the two to be, and whichever it picks, the other half of the design goes unseen.
        // member@ stays self-directed; member2@ is coached and on a written programme.
        var coachedMember = new User
        {
            TenantId = tenantId,
            Email = "member2@titanfitness.demo",
            PasswordHash = passwordHash,
            FirstName = "Coached",
            LastName = "Member",
            IsActive = true
        };
        db.Users.Add(coachedMember);
        users[CoachedMemberKey] = coachedMember;

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (key, user) in users)
        {
            var roleName = key == CoachedMemberKey ? RoleNames.Member : key;
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

    /// <summary>
    /// Which role gets which permission codes — the declared authority matrix.
    ///
    /// Extracted from the seed path so the catalogue SYNC can reuse it. A brand-new permission
    /// code has to reach the roles that are supposed to hold it on an ALREADY-SEEDED database,
    /// otherwise it exists as a row nobody is granted and the feature behind it 403s for everyone.
    /// </summary>
    /// <param name="allCodes">Every code in the catalogue — Owner and Manager are defined as
    /// catch-alls over it rather than as hand-listed sets that would go stale on every addition.</param>
    private static Dictionary<string, string[]> GetRolePermissionMap(IEnumerable<string> allCodes)
    {
        var permissionCodes = allCodes.ToList();
        return new Dictionary<string, string[]>
        {
            // Portal.View is the member self-service grant, not a staff capability — its own catalog
            // entry reads "view OWN member profile". Owner/Manager are staff and are never linked to
            // a Member row, so handing it to them (as a side effect of "all permissions") only put
            // three dead sidebar links — My Account / My Classes / My Progress — that can render
            // nothing but "ask the front desk to link your account". Excluded from every staff role;
            // the Member role below is the only one that gets it.
            //
            // Settings.ManageStaff (hiring and deactivating logins) reaches Owner and Manager through
            // these two catch-alls and nobody else, which is deliberate: it is the permission that can
            // create an account holding any other permission, so a Receptionist who could grant it to
            // themselves would make every other line in this map advisory.
            [RoleNames.Owner] = permissionCodes.Where(c => c != PermissionCodes.Portal.View).ToArray(),
            [RoleNames.Manager] = permissionCodes
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
    }

    /// <summary>
    /// Brings the Permission rows in an EXISTING database into line with the code catalogue, and
    /// grants brand-new codes to the roles the authority matrix says should hold them.
    ///
    /// Idempotent and safe to run on every start: it adds what is missing, removes what was retired,
    /// and otherwise leaves the database alone.
    ///
    /// WHAT IT DELIBERATELY DOES NOT DO: re-apply the whole matrix. Grants are only written for
    /// codes this run just created. An owner who revoked something through the permission matrix
    /// made a decision, and a sync that reinstated it on every deploy would silently overrule them —
    /// which is a worse failure than the one this method exists to fix.
    /// </summary>
    private async Task SyncPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = GetPermissionCatalog();
        var catalogCodes = catalog.Select(c => c.Code).ToHashSet();

        var existing = await db.Permissions.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var existingCodes = existing.Select(p => p.Code).ToHashSet();

        // Retired codes: drop their grants first, since RolePermission has a required FK to them.
        var retired = existing.Where(p => !catalogCodes.Contains(p.Code)).ToList();
        if (retired.Count > 0)
        {
            var retiredIds = retired.Select(p => p.Id).ToList();
            var deadGrants = await db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => retiredIds.Contains(rp.PermissionId))
                .ToListAsync(cancellationToken);

            db.RolePermissions.RemoveRange(deadGrants);
            db.Permissions.RemoveRange(retired);
            logger.LogInformation(
                "Retiring {Count} permission code(s) no longer in the catalogue: {Codes}",
                retired.Count, string.Join(", ", retired.Select(p => p.Code)));
        }

        var added = catalog
            .Where(c => !existingCodes.Contains(c.Code))
            .Select(c => new Permission { Code = c.Code, Module = c.Module, Description = c.Description })
            .ToList();

        if (added.Count > 0)
        {
            db.Permissions.AddRange(added);
            logger.LogInformation(
                "Adding {Count} new permission code(s): {Codes}", added.Count, string.Join(", ", added.Select(p => p.Code)));
        }

        if (retired.Count == 0 && added.Count == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (added.Count == 0)
        {
            return;
        }

        // Grant the new codes per the matrix, for every tenant — Role is tenant-scoped, so a
        // multi-tenant database has one Owner row per gym and each needs its own grant.
        var addedByCode = added.ToDictionary(p => p.Code);
        var roles = await db.Roles.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var map = GetRolePermissionMap(catalogCodes);

        foreach (var role in roles)
        {
            if (!map.TryGetValue(role.Name, out var codesForRole))
            {
                continue;
            }

            foreach (var code in codesForRole.Where(addedByCode.ContainsKey))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = addedByCode[code].Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// AssignClientCommand shipped without stamping TenantId on the TrainerAssignment it creates.
    /// TrainerAssignment is tenant-scoped, so every row it wrote landed with the column defaulted to
    /// Guid.Empty — invisible to the global query filter the instant it was saved, in every query that
    /// reads assignments, not just display. Not a bug that can be fixed by editing the row through the
    /// app either: the same filter blocks the handler that would end or reassign it. Run on every
    /// deploy, before the "already seeded" bail-out, so an affected production database repairs itself
    /// without a manual migration or direct database access.
    ///
    /// The correct tenant is derived from the Trainer the row references — Trainer.TenantId was always
    /// stamped correctly by CreateTrainerCommand — rather than assumed to be "the only tenant", so this
    /// stays correct even if a deployment ever has more than one.
    /// </summary>
    private async Task BackfillOrphanedTrainerAssignmentsAsync(CancellationToken cancellationToken)
    {
        var orphaned = await db.TrainerAssignments.IgnoreQueryFilters()
            .Where(a => a.TenantId == Guid.Empty)
            .ToListAsync(cancellationToken);

        if (orphaned.Count == 0)
        {
            return;
        }

        var trainerIds = orphaned.Select(a => a.TrainerId).Distinct().ToList();
        var trainerTenants = await db.Trainers.IgnoreQueryFilters()
            .Where(t => trainerIds.Contains(t.Id))
            .Select(t => new { t.Id, t.TenantId })
            .ToDictionaryAsync(t => t.Id, t => t.TenantId, cancellationToken);

        var repaired = 0;
        foreach (var assignment in orphaned)
        {
            if (trainerTenants.TryGetValue(assignment.TrainerId, out var tenantId))
            {
                assignment.TenantId = tenantId;
                repaired++;
            }
        }

        if (repaired == 0)
        {
            return;
        }

        logger.LogInformation(
            "Repairing {Count} trainer assignment(s) created before TenantId was stamped on write.", repaired);
        await db.SaveChangesAsync(cancellationToken);
    }

}

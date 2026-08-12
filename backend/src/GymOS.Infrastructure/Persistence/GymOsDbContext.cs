using System.Linq.Expressions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Attendance;
using GymOS.Domain.Auditing;
using GymOS.Domain.Billing;
using GymOS.Domain.Classes;
using GymOS.Domain.Common;
using GymOS.Domain.Crm;
using GymOS.Domain.Equipment;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Inventory;
using GymOS.Domain.Maintenance;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Migration;
using GymOS.Domain.Notifications;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Settings;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GymOS.Infrastructure.Persistence;

public class GymOsDbContext(
    DbContextOptions<GymOsDbContext> options,
    ITenantProvider tenantProvider,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IPublisher publisher)
    : DbContext(options), IApplicationDbContext
{
    // Tenancy / Settings
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<GymProfile> GymProfiles => Set<GymProfile>();
    public DbSet<SystemPreference> SystemPreferences => Set<SystemPreference>();

    // Identity / RBAC
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Members
    public DbSet<Member> Members => Set<Member>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<MedicalNote> MedicalNotes => Set<MedicalNote>();

    public DbSet<MemberNote> MemberNotes => Set<MemberNote>();
    public DbSet<MemberMeasurement> MemberMeasurements => Set<MemberMeasurement>();
    public DbSet<ProgressPhoto> ProgressPhotos => Set<ProgressPhoto>();
    public DbSet<MemberMembership> MemberMemberships => Set<MemberMembership>();
    public DbSet<MemberGoal> MemberGoals => Set<MemberGoal>();

    public DbSet<MemberTrainingPreference> MemberTrainingPreferences => Set<MemberTrainingPreference>();

    // Memberships
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Coupon> Coupons => Set<Coupon>();

    // Billing
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<PaymentReminder> PaymentReminders => Set<PaymentReminder>();
    public DbSet<RecurringBillingAttempt> RecurringBillingAttempts => Set<RecurringBillingAttempt>();

    // Attendance
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // Notifications
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<ScheduledNotification> ScheduledNotifications => Set<ScheduledNotification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    // Auditing
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // CRM (Wave 2)
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();

    // Trainers (Wave 2)
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<TrainerAssignment> TrainerAssignments => Set<TrainerAssignment>();

    public DbSet<CoachMessage> CoachMessages => Set<CoachMessage>();
    public DbSet<TrainerSchedule> TrainerSchedules => Set<TrainerSchedule>();
    public DbSet<TrainerRating> TrainerRatings => Set<TrainerRating>();
    public DbSet<TrainerSession> TrainerSessions => Set<TrainerSession>();
    public DbSet<CommissionRecord> CommissionRecords => Set<CommissionRecord>();

    // Equipment (Wave 2)
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    // Maintenance (Wave 2)
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<DowntimeLog> DowntimeLogs => Set<DowntimeLog>();

    // Inventory (Wave 2)
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PurchaseRecord> PurchaseRecords => Set<PurchaseRecord>();

    // Workouts (Wave 3)
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises => Set<WorkoutTemplateExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<WorkoutLogEntry> WorkoutLogEntries => Set<WorkoutLogEntry>();
    public DbSet<WorkoutAssignment> WorkoutAssignments => Set<WorkoutAssignment>();

    // Nutrition (Wave 3)
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<DietPlanGuidance> DietPlanGuidance => Set<DietPlanGuidance>();
    public DbSet<PlanAdherenceLog> PlanAdherenceLogs => Set<PlanAdherenceLog>();
    public DbSet<WaterLog> WaterLogs => Set<WaterLog>();

    // Migration Center (Wave 3)
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportFieldMapping> ImportFieldMappings => Set<ImportFieldMapping>();

    // Classes / Scheduling
    public DbSet<ClassType> ClassTypes => Set<ClassType>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<ClassBooking> ClassBookings => Set<ClassBooking>();

    // Member Experience Engine
    public DbSet<MemberProgression> MemberProgressions => Set<MemberProgression>();
    public DbSet<XpTransaction> XpTransactions => Set<XpTransaction>();
    public DbSet<PersonalRecord> PersonalRecords => Set<PersonalRecord>();
    public DbSet<ExerciseMastery> ExerciseMasteries => Set<ExerciseMastery>();
    public DbSet<MemberAchievement> MemberAchievements => Set<MemberAchievement>();

    public DbSet<RankPromotion> RankPromotions => Set<RankPromotion>();
    public DbSet<RecoveryLog> RecoveryLogs => Set<RecoveryLog>();
    public DbSet<SkillTree> SkillTrees => Set<SkillTree>();
    public DbSet<SkillNode> SkillNodes => Set<SkillNode>();
    public DbSet<CommunityChallenge> CommunityChallenges => Set<CommunityChallenge>();
    public DbSet<ChallengeParticipant> ChallengeParticipants => Set<ChallengeParticipant>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Domain events are a behavioural concern dispatched from SaveChanges, never persisted — keep
        // AggregateRoot.DomainEvents out of the model so EF doesn't try to map it as a navigation.
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymOsDbContext).Assembly);

        ConvertEnumsToStrings(modelBuilder);
        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applied once, model-wide, instead of per-entity HasConversion&lt;string&gt;() calls in
    /// every configuration class — keeps enum columns human-readable in the DB (e.g. "Active"
    /// instead of 0) without repeating the same line ~40 times across modules.
    /// </summary>
    private static void ConvertEnumsToStrings(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.ClrType.GetProperties())
            {
                var propertyType = property.PropertyType;
                var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

                if (!underlyingType.IsEnum)
                {
                    continue;
                }

                modelBuilder.Entity(entityType.ClrType).Property(property.Name).HasConversion<string>();
            }
        }
    }

    /// <summary>
    /// Tenant AND branch isolation are enforced here, model-wide, rather than trusting every handler
    /// to remember a Where clause.
    ///
    /// Branch used to be excluded on the grounds that "Owner/Manager can span multiple branches", so
    /// scoping was left to an explicit query parameter plus a pipeline behaviour. That reasoning does
    /// not hold: filtering on the SET of accessible branches handles a multi-branch manager fine —
    /// their set simply contains every branch. And the behaviour could only ever inspect a property
    /// literally named BranchId on the REQUEST, which meant it protected nothing addressed by its own
    /// id. Confirmed live: a Receptionist scoped to one branch could not see another branch's member
    /// in the list, and could read that same member's name, date of birth and home address by
    /// fetching them by id. The same hole let an Accountant record a payment against another
    /// branch's invoice.
    ///
    /// Doing it here closes reads, reads-by-id and writes-by-id in one place, because a write path
    /// loads its entity through the same filtered DbSet and now simply cannot find a foreign row —
    /// it gets a NotFound rather than silently mutating another branch's data.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var parameter = Expression.Parameter(clrType, "e");
            Expression? filter = null;

            if (typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                /*
                 * FAILS CLOSED, AND DELIBERATELY UNLIKE THE BRANCH FILTER BELOW.
                 *
                 * With no tenant context CurrentTenantId is null, so this compares TenantId to null and
                 * matches nothing. That is the intended behaviour, and it is worth stating because the
                 * branch filter immediately below does the opposite — it carries an explicit flag so
                 * that no branch context means ALL branches — and the asymmetry looks like an oversight
                 * until you see why it isn't.
                 *
                 * Within a tenant, a background job legitimately works across every branch, so
                 * fail-open is convenient and harmless there. Across tenants it is neither. A job that
                 * forgot to scope would go from returning nothing — an obvious bug the first time
                 * anyone runs it — to silently reading and writing every customer's data. The cheap
                 * failure is the one worth keeping.
                 *
                 * Background jobs therefore do NOT get an escape hatch here. They do what every job in
                 * BackgroundJobs/ already does: enumerate tenants and scope each pass explicitly with
                 * IgnoreQueryFilters(), which is a visible decision at the call site rather than an
                 * ambient default nobody reads.
                 *
                 * TenantIsolationTests pins both halves of this so neither can be "tidied up" into the
                 * other.
                 */
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantIdAsNullable = Expression.Convert(tenantIdProperty, typeof(Guid?));
                var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                filter = Expression.Equal(tenantIdAsNullable, currentTenantId);
            }

            if (typeof(IBranchScoped).IsAssignableFrom(clrType))
            {
                /*
                 * Reads as: !BranchScopeEnabled || AccessibleBranchIds.Contains(e.BranchId)
                 *
                 * Written as a bool flag beside a never-null list rather than a null check on the
                 * list, because `@list IS NULL` does not survive translation the way it reads in C# —
                 * SQL's NULL comparison would make the escape hatch silently false and hide every
                 * row from every background job. The flag parameterises to `NOT @enabled OR ... IN`,
                 * which short-circuits correctly in SQL.
                 */
                var branchIdProperty = Expression.Property(parameter, nameof(IBranchScoped.BranchId));
                var accessible = Expression.Property(Expression.Constant(this), nameof(CurrentAccessibleBranchIds));
                var contains = Expression.Call(
                    typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], accessible, branchIdProperty);

                var scopeDisabled = Expression.Not(
                    Expression.Property(Expression.Constant(this), nameof(BranchScopeEnabled)));

                var branchFilter = Expression.OrElse(scopeDisabled, contains);
                filter = filter is null ? branchFilter : Expression.AndAlso(filter, branchFilter);
            }

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var notDeleted = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                filter = filter is null ? notDeleted : Expression.AndAlso(filter, notDeleted);
            }

            if (filter is not null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter, parameter));
            }
        }
    }

    // Referenced via reflection (nameof(CurrentTenantId)) inside the query filter expression above.
    private Guid? CurrentTenantId => tenantProvider.TenantId;

    /// <summary>
    /// False in the system context (background jobs, seeding, unauthenticated) — see
    /// ITenantProvider.AccessibleBranchIds for why null and empty must not mean the same thing.
    /// </summary>
    private bool BranchScopeEnabled => tenantProvider.AccessibleBranchIds is not null;

    /// <summary>
    /// Never null, so the Contains call in the filter expression is always translatable; when scope
    /// is disabled the flag above short-circuits it before the contents matter.
    /// </summary>
    private IReadOnlyList<Guid> CurrentAccessibleBranchIds => tenantProvider.AccessibleBranchIds ?? [];

    /// <summary>
    /// Takes a PostgreSQL row lock on one invoice, held until the ambient transaction ends. See
    /// IApplicationDbContext.LockInvoiceForUpdateAsync for why the billing handlers need it.
    ///
    /// Raw SQL rather than EF, because there is no LINQ for FOR UPDATE. That means it bypasses the
    /// tenant and branch query filters — which is fine and deliberate: this locks a row by primary
    /// key and reads nothing back. The very next statement in every caller is a filtered EF query
    /// that still decides whether the caller may see that invoice at all, so nothing is disclosed by
    /// having briefly locked a row. Locking is not authorisation.
    ///
    /// A missing or invisible id locks nothing and is not an error here; the caller's own query is
    /// what turns that into a 404.
    /// </summary>
    public async Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        /*
         * A no-op on anything but PostgreSQL, and that is load-bearing rather than a shrug.
         *
         * The Application test suite runs on SQLite in-memory over ONE shared connection
         * (ApplicationTestBase), which has no FOR UPDATE and already serialises every write on that
         * connection — so the statement would be a syntax error in exchange for a guarantee the
         * harness provides anyway. The real concurrency test lives in Api.IntegrationTests, which
         * runs against real Postgres; see PaymentConcurrencyTests.
         */
        if (!Database.IsNpgsql())
        {
            return;
        }

        await Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM "Invoices" WHERE "Id" = {invoiceId} FOR UPDATE""",
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedByUserId = currentUser.UserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = currentUser.UserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty && currentUser.TenantId is not null)
            {
                entry.Entity.TenantId = currentUser.TenantId.Value;
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events AFTER the source rows are persisted, so handlers see committed
        // state. Handler writes (XP ledger, projections) join the same ambient transaction that
        // TransactionBehavior already opened, so the trigger and its projections commit atomically.
        await DispatchDomainEventsAsync(cancellationToken);

        return result;
    }

    private bool _dispatchingDomainEvents;

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        // Re-entrancy guard: the follow-up saves below must not recurse back into dispatch via
        // this.SaveChangesAsync — we own the whole multi-pass dispatch here.
        if (_dispatchingDomainEvents)
        {
            return;
        }

        _dispatchingDomainEvents = true;
        try
        {
            // Loop, not a single pass: a handler can raise a follow-up event on a tracked aggregate
            // (e.g. awarding XP makes MemberProgression raise MemberProgressionChanged), and that
            // event must be dispatched too — after the first pass's writes are committed, so its
            // handlers read fresh state. Terminates when a pass raises no new events; handlers on the
            // terminal events (achievements) don't re-raise.
            while (true)
            {
                var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
                    .Where(e => e.Entity.DomainEvents.Count > 0)
                    .Select(e => e.Entity)
                    .ToList();

                if (aggregates.Count == 0)
                {
                    break;
                }

                var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();
                aggregates.ForEach(a => a.ClearDomainEvents());

                foreach (var domainEvent in domainEvents)
                {
                    // Wrap in the Application-layer adapter so Domain stays MediatR-free; handlers
                    // subscribe to DomainEventNotification<TEvent>.
                    var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
                    var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
                    await publisher.Publish(notification, cancellationToken);
                }

                // Persist this pass's handler writes before the next pass, so follow-up handlers see
                // committed state. base.SaveChanges (not this) to skip re-stamping and re-dispatch;
                // handler-written rows set their own TenantId/timestamps.
                if (ChangeTracker.HasChanges())
                {
                    await base.SaveChangesAsync(cancellationToken);
                }
            }
        }
        finally
        {
            _dispatchingDomainEvents = false;
        }
    }
}

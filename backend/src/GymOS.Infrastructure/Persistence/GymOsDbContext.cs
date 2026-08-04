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
    public DbSet<MemberMeasurement> MemberMeasurements => Set<MemberMeasurement>();
    public DbSet<ProgressPhoto> ProgressPhotos => Set<ProgressPhoto>();
    public DbSet<MemberMembership> MemberMemberships => Set<MemberMembership>();
    public DbSet<MemberGoal> MemberGoals => Set<MemberGoal>();

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
    /// Tenant isolation is enforced here, model-wide, rather than trusting every handler to
    /// remember a Where(x => x.TenantId == ...) clause. Branch is deliberately NOT filtered here —
    /// Owner/Manager can span multiple branches, so branch scoping stays an explicit, optional
    /// query parameter in Application handlers instead of a blanket DB-level filter.
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
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantIdAsNullable = Expression.Convert(tenantIdProperty, typeof(Guid?));
                var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                filter = Expression.Equal(tenantIdAsNullable, currentTenantId);
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
        // Re-entrancy guard: the follow-up save below can itself be a no-op or (in future slices)
        // raise further events; we only ever run one dispatch pass per outermost SaveChanges.
        if (_dispatchingDomainEvents)
        {
            return;
        }

        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        _dispatchingDomainEvents = true;
        try
        {
            foreach (var domainEvent in domainEvents)
            {
                // Wrap in the Application-layer adapter so Domain stays MediatR-free; handlers
                // subscribe to DomainEventNotification<TEvent>.
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
                var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
                await publisher.Publish(notification, cancellationToken);
            }

            // Persist whatever the handlers changed (XP ledger, MemberProgression). base.SaveChanges,
            // not this.SaveChanges, so we don't re-run the stamping pass or recurse into dispatch —
            // handler-written rows set their own TenantId/timestamps.
            if (ChangeTracker.HasChanges())
            {
                await base.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _dispatchingDomainEvents = false;
        }
    }
}

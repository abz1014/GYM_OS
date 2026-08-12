using GymOS.Domain.Attendance;
using GymOS.Domain.Auditing;
using GymOS.Domain.Billing;
using GymOS.Domain.Classes;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Query surface for every module — handlers query these DbSets directly via LINQ rather than
/// going through IRepository, which is reserved for writes. Implemented by GymOsDbContext in
/// Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DatabaseFacade Database { get; }

    /// <summary>
    /// Serialises every money-writing path against one invoice, for the life of the current
    /// transaction.
    ///
    /// The overpayment ceiling is a read-then-write: sum what has been paid, compare, insert. Under
    /// READ COMMITTED that is not one operation, so N simultaneous requests all read the same
    /// balance and all pass. Proven, not theorised: six concurrent full payments against a $100
    /// invoice were all accepted, leaving $600 recorded against it, and eight concurrent refunds
    /// took $800 back out of a $100 payment. Issued sequentially the same requests are correctly
    /// refused, which is what makes it a race rather than a logic error.
    ///
    /// Every caller must take this BEFORE reading the balance it is about to act on — a lock
    /// acquired after the read protects nothing. The invoice is the single lock target for both
    /// payments and refunds even though a refund's ceiling is per-payment, because a payment's
    /// ceiling reads refunds: one target for the pair means there is no lock ordering to get wrong
    /// and therefore no deadlock to reason about.
    /// </summary>
    Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    DbSet<Tenant> Tenants { get; }

    DbSet<Branch> Branches { get; }

    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<UserBranchAccess> UserBranchAccesses { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<Member> Members { get; }

    DbSet<EmergencyContact> EmergencyContacts { get; }

    DbSet<MedicalNote> MedicalNotes { get; }

    DbSet<MemberNote> MemberNotes { get; }

    DbSet<MemberMeasurement> MemberMeasurements { get; }

    DbSet<ProgressPhoto> ProgressPhotos { get; }

    DbSet<MemberMembership> MemberMemberships { get; }

    DbSet<MemberGoal> MemberGoals { get; }

    DbSet<MemberTrainingPreference> MemberTrainingPreferences { get; }

    DbSet<MembershipPlan> MembershipPlans { get; }

    DbSet<Discount> Discounts { get; }

    DbSet<Coupon> Coupons { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<InvoiceLine> InvoiceLines { get; }

    DbSet<Payment> Payments { get; }

    DbSet<Refund> Refunds { get; }

    DbSet<PaymentReminder> PaymentReminders { get; }

    DbSet<RecurringBillingAttempt> RecurringBillingAttempts { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<NotificationTemplate> NotificationTemplates { get; }

    DbSet<ScheduledNotification> ScheduledNotifications { get; }

    DbSet<NotificationLog> NotificationLogs { get; }

    DbSet<GymProfile> GymProfiles { get; }

    DbSet<SystemPreference> SystemPreferences { get; }

    DbSet<AuditLog> AuditLogs { get; }

    // CRM (Wave 2)
    DbSet<Lead> Leads { get; }

    DbSet<LeadActivity> LeadActivities { get; }

    // Trainers (Wave 2)
    DbSet<Trainer> Trainers { get; }

    DbSet<TrainerAssignment> TrainerAssignments { get; }

    DbSet<CoachMessage> CoachMessages { get; }

    DbSet<TrainerSchedule> TrainerSchedules { get; }

    DbSet<TrainerRating> TrainerRatings { get; }

    DbSet<TrainerSession> TrainerSessions { get; }

    DbSet<CommissionRecord> CommissionRecords { get; }

    // Equipment (Wave 2)
    DbSet<Asset> Assets { get; }

    DbSet<Supplier> Suppliers { get; }

    // Maintenance (Wave 2)
    DbSet<WorkOrder> WorkOrders { get; }

    DbSet<MaintenanceSchedule> MaintenanceSchedules { get; }

    DbSet<DowntimeLog> DowntimeLogs { get; }

    // Inventory (Wave 2)
    DbSet<InventoryItem> InventoryItems { get; }

    DbSet<StockMovement> StockMovements { get; }

    DbSet<PurchaseRecord> PurchaseRecords { get; }

    // Workouts (Wave 3)
    DbSet<Exercise> Exercises { get; }

    /// <summary>Which muscle groups each movement works, primary and secondary. See ExerciseMuscle.</summary>
    DbSet<ExerciseMuscle> ExerciseMuscles { get; }

    DbSet<WorkoutTemplate> WorkoutTemplates { get; }

    DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises { get; }

    DbSet<WorkoutLog> WorkoutLogs { get; }

    DbSet<WorkoutLogEntry> WorkoutLogEntries { get; }

    DbSet<WorkoutAssignment> WorkoutAssignments { get; }

    // Nutrition (Wave 3)
    DbSet<FoodItem> FoodItems { get; }

    DbSet<DietPlan> DietPlans { get; }

    DbSet<MealEntry> MealEntries { get; }

    /// <summary>What the nutritionist wants the member to do this week and this month.</summary>
    DbSet<DietPlanGuidance> DietPlanGuidance { get; }

    /// <summary>One row per day a member confirmed they stayed on plan. See PlanAdherenceLog.</summary>
    DbSet<PlanAdherenceLog> PlanAdherenceLogs { get; }

    DbSet<WaterLog> WaterLogs { get; }

    // Migration Center (Wave 3)
    DbSet<ImportJob> ImportJobs { get; }

    DbSet<ImportRow> ImportRows { get; }

    DbSet<ImportFieldMapping> ImportFieldMappings { get; }

    // Classes / Scheduling
    DbSet<ClassType> ClassTypes { get; }

    DbSet<ClassSchedule> ClassSchedules { get; }

    DbSet<ClassSession> ClassSessions { get; }

    DbSet<ClassBooking> ClassBookings { get; }

    // Member Experience Engine
    DbSet<MemberProgression> MemberProgressions { get; }

    DbSet<XpTransaction> XpTransactions { get; }

    DbSet<PersonalRecord> PersonalRecords { get; }

    DbSet<ExerciseMastery> ExerciseMasteries { get; }

    DbSet<MemberAchievement> MemberAchievements { get; }

    /// <summary>Append-only record of every rung a member has reached; unique per (member, tier).</summary>
    DbSet<RankPromotion> RankPromotions { get; }

    DbSet<RecoveryLog> RecoveryLogs { get; }

    DbSet<SkillTree> SkillTrees { get; }

    DbSet<SkillNode> SkillNodes { get; }

    DbSet<CommunityChallenge> CommunityChallenges { get; }

    DbSet<ChallengeParticipant> ChallengeParticipants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

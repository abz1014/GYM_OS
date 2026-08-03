using GymOS.Domain.Attendance;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Notifications;
using GymOS.Domain.Settings;
using GymOS.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Read-side query surface for Wave 1 modules — handlers query these DbSets directly via LINQ
/// rather than going through IRepository, which is reserved for writes. Implemented by
/// GymOsDbContext in Infrastructure, which also holds the DbSets for every other module so the
/// full schema is scaffolded even though only Wave 1 has query handlers today.
/// </summary>
public interface IApplicationDbContext
{
    DatabaseFacade Database { get; }

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

    DbSet<MemberMeasurement> MemberMeasurements { get; }

    DbSet<ProgressPhoto> ProgressPhotos { get; }

    DbSet<MemberMembership> MemberMemberships { get; }

    DbSet<MembershipPlan> MembershipPlans { get; }

    DbSet<Discount> Discounts { get; }

    DbSet<Coupon> Coupons { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<InvoiceLine> InvoiceLines { get; }

    DbSet<Payment> Payments { get; }

    DbSet<Refund> Refunds { get; }

    DbSet<PaymentReminder> PaymentReminders { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<NotificationTemplate> NotificationTemplates { get; }

    DbSet<ScheduledNotification> ScheduledNotifications { get; }

    DbSet<NotificationLog> NotificationLogs { get; }

    DbSet<GymProfile> GymProfiles { get; }

    DbSet<SystemPreference> SystemPreferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

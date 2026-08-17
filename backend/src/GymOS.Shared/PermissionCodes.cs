using System.Reflection;

namespace GymOS.Shared;

/// <summary>
/// Canonical permission codes ("module.action"). Used both to seed the Permission catalog and as
/// the literal string passed to [RequirePermission] on API controller actions, so the two never drift.
/// </summary>
public static class PermissionCodes
{
    /// <summary>Every permission code declared below, discovered via reflection so Program.cs can register one authorization policy per code without hand-maintaining a second list.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(PermissionCodes)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(nested => nested.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(field => field.FieldType == typeof(string))
        .Select(field => (string)field.GetValue(null)!)
        .ToList();

    public static class Members
    {
        public const string View = "members.view";
        public const string Create = "members.create";
        public const string Update = "members.update";
        // No "members.delete" code lives here. One used to, and it granted nothing: no controller
        // action ever referenced it, but because this class is the source for both the authorization
        // policies and the seeded catalog, it still rendered as a live switch in the owner's
        // permission matrix. An owner could turn "Delete members" on or off all afternoon and change
        // nothing. Members are retired by soft-delete/membership status, not by a delete endpoint —
        // if one is ever added, add the code back alongside it, not before.
        public const string ManageMembership = "members.manage_membership";
    }

    public static class Memberships
    {
        public const string View = "memberships.view";
        public const string ManagePlans = "memberships.manage_plans";
        public const string ManageDiscounts = "memberships.manage_discounts";
    }

    public static class Billing
    {
        public const string View = "billing.view";
        public const string CreateInvoice = "billing.create_invoice";
        public const string RecordPayment = "billing.record_payment";
        public const string IssueRefund = "billing.issue_refund";
    }

    public static class Attendance
    {
        public const string View = "attendance.view";
        public const string CheckIn = "attendance.check_in";
    }

    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    /// <summary>Gates the member self-service portal (own profile/attendance/workouts/nutrition
    /// only — every Portal query resolves "whose data" server-side from the JWT, never from a
    /// caller-supplied id). Deliberately separate from Attendance/Workouts/Nutrition.View, which
    /// grant staff-wide access to every member's records.</summary>
    public static class Portal
    {
        public const string View = "portal.view";
    }

    public static class Settings
    {
        public const string View = "settings.view";
        public const string ManageBranches = "settings.manage_branches";
        public const string ManagePermissions = "settings.manage_permissions";
        public const string ManageGymProfile = "settings.manage_gym_profile";

        /// <summary>Hiring, editing and deactivating staff logins. Distinct from ManagePermissions,
        /// which decides what a role may do; this decides who holds a role at all.</summary>
        public const string ManageStaff = "settings.manage_staff";
    }

    public static class Crm
    {
        public const string View = "crm.view";
        public const string ManageLeads = "crm.manage_leads";
    }

    public static class Trainers
    {
        public const string View = "trainers.view";
        public const string Manage = "trainers.manage";
    }

    public static class Classes
    {
        public const string View = "classes.view";
        public const string Manage = "classes.manage";
    }

    public static class Equipment
    {
        public const string View = "equipment.view";
        public const string Manage = "equipment.manage";
    }

    public static class Maintenance
    {
        public const string View = "maintenance.view";
        public const string Manage = "maintenance.manage";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string Manage = "inventory.manage";
    }

    public static class Workouts
    {
        public const string View = "workouts.view";
        public const string Manage = "workouts.manage";
    }

    public static class Nutrition
    {
        public const string View = "nutrition.view";
        public const string Manage = "nutrition.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Notifications
    {
        public const string View = "notifications.view";
        public const string Manage = "notifications.manage";
    }

    public static class Migration
    {
        public const string Manage = "migration.manage";
    }

    /// <summary>Staff management of Member Experience Engine content (currently: creating community
    /// challenges). Distinct from Portal.View, which gates the member's own self-service surface.</summary>
    public static class Experience
    {
        public const string Manage = "experience.manage";
    }
}

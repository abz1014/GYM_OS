import type { LucideIcon } from 'lucide-react'
import {
  MessageSquare, Activity, Apple, BarChart3, Bell, CalendarDays, CreditCard, Dumbbell, Flag, Hammer, LayoutDashboard, Package, QrCode, Receipt, Settings, Target, UploadCloud, Users, Wrench } from 'lucide-react'

/**
 * Which cluster a module belongs to in the sidebar. Named after the JOB the person doing it is on
 * rather than the department that owns it — someone opening this console is running a shift, chasing
 * money, or looking after the building, and those are three different frames of mind. Anything that
 * isn't one of those three falls to `More`, which the sidebar collapses.
 */
export type NavSection = 'Operate' | 'Revenue' | 'Facility' | 'More'

export const NAV_SECTIONS: NavSection[] = ['Operate', 'Revenue', 'Facility']

export interface NavModule {
  key: string
  label: string
  path: string
  icon: LucideIcon
  permission: string
  section: NavSection
  /**
   * A role the account must actually HOLD, on top of the permission.
   *
   * Only "My clients" needs this so far, and it needs it because permission and identity come apart
   * there: an owner has trainers.view so they can manage the trainer roster, but they are not
   * anybody's coach, and /api/coaching/clients resolves the acting trainer from the JWT and refuses
   * an account with no Trainer row. Showing them the entry would offer a screen that can only fail.
   */
  requiresRole?: string
}

/**
 * The STAFF sidebar. Member navigation is not here — a member-only login renders MemberTabBar
 * instead of the sidebar entirely (see AppShell / shared/nav/memberNav.ts), so portal.view entries
 * in this list could never be shown to anyone: members don't get a sidebar, and no staff role is
 * granted portal.view.
 *
 * Order within a section is the order it renders. The `wave` field is gone: every module shipped, so
 * the sidebar's "Coming soon" branch had been unreachable for a while and was describing a state the
 * product no longer has.
 */
export const NAV_MODULES: NavModule[] = [
  { key: 'dashboard', label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, permission: 'dashboard.view', section: 'Operate' },
  { key: 'members', label: 'Members', path: '/members', icon: Users, permission: 'members.view', section: 'Operate' },
  // "Front desk" rather than "Attendance": the screen is a kiosk someone stands at, and that is what
  // the person looking for it in this list calls it.
  { key: 'attendance', label: 'Front desk', path: '/attendance', icon: QrCode, permission: 'attendance.view', section: 'Operate' },
  { key: 'classes', label: 'Classes', path: '/classes', icon: CalendarDays, permission: 'classes.view', section: 'Operate' },
  { key: 'trainers', label: 'Trainers', path: '/trainers', icon: Dumbbell, permission: 'trainers.view', section: 'Operate' },
  { key: 'my-clients', label: 'My clients', path: '/my-clients', icon: MessageSquare, permission: 'trainers.view', section: 'Operate', requiresRole: 'Trainer' },

  { key: 'billing', label: 'Billing', path: '/billing', icon: Receipt, permission: 'billing.view', section: 'Revenue' },
  { key: 'memberships', label: 'Memberships', path: '/memberships', icon: CreditCard, permission: 'memberships.view', section: 'Revenue' },
  { key: 'crm', label: 'Leads & CRM', path: '/crm', icon: Target, permission: 'crm.view', section: 'Revenue' },

  { key: 'equipment', label: 'Equipment', path: '/equipment', icon: Wrench, permission: 'equipment.view', section: 'Facility' },
  { key: 'maintenance', label: 'Maintenance', path: '/maintenance', icon: Hammer, permission: 'maintenance.view', section: 'Facility' },

  { key: 'inventory', label: 'Inventory', path: '/inventory', icon: Package, permission: 'inventory.view', section: 'More' },
  { key: 'workouts', label: 'Workouts', path: '/workouts', icon: Activity, permission: 'workouts.view', section: 'More' },
  { key: 'nutrition', label: 'Nutrition', path: '/nutrition', icon: Apple, permission: 'nutrition.view', section: 'More' },
  { key: 'reports', label: 'Reports', path: '/reports', icon: BarChart3, permission: 'reports.view', section: 'More' },
  { key: 'notifications', label: 'Notification Center', path: '/notifications', icon: Bell, permission: 'notifications.view', section: 'More' },
  { key: 'challenges', label: 'Community Challenges', path: '/challenges', icon: Flag, permission: 'experience.manage', section: 'More' },
  { key: 'migration', label: 'Migration Center', path: '/migration', icon: UploadCloud, permission: 'migration.manage', section: 'More' },
  { key: 'settings', label: 'Settings', path: '/settings', icon: Settings, permission: 'settings.view', section: 'More' },
]

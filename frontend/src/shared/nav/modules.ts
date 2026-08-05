import type { LucideIcon } from 'lucide-react'
import { Activity, Apple, BarChart3, Bell, CalendarDays, CreditCard, Dumbbell, Flag, Hammer, LayoutDashboard, Package, QrCode, Receipt, Settings, Target, UploadCloud, Users, Wrench } from 'lucide-react'

export interface NavModule {
  key: string
  label: string
  path: string
  icon: LucideIcon
  permission: string
  wave: 1 | 2 | 3
}

/**
 * The STAFF sidebar. Member navigation is not here — a member-only login renders MemberTabBar
 * instead of the sidebar entirely (see AppShell / shared/nav/memberNav.ts), so portal.view entries
 * in this list could never be shown to anyone: members don't get a sidebar, and no staff role is
 * granted portal.view.
 */
export const NAV_MODULES: NavModule[] = [
  { key: 'dashboard', label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, permission: 'dashboard.view', wave: 1 },
  { key: 'members', label: 'Members', path: '/members', icon: Users, permission: 'members.view', wave: 1 },
  { key: 'memberships', label: 'Memberships', path: '/memberships', icon: CreditCard, permission: 'memberships.view', wave: 1 },
  { key: 'attendance', label: 'Attendance', path: '/attendance', icon: QrCode, permission: 'attendance.view', wave: 1 },
  { key: 'billing', label: 'Billing & Invoicing', path: '/billing', icon: Receipt, permission: 'billing.view', wave: 1 },
  { key: 'crm', label: 'CRM & Leads', path: '/crm', icon: Target, permission: 'crm.view', wave: 1 },
  { key: 'trainers', label: 'Trainers', path: '/trainers', icon: Dumbbell, permission: 'trainers.view', wave: 1 },
  { key: 'classes', label: 'Classes', path: '/classes', icon: CalendarDays, permission: 'classes.view', wave: 1 },
  { key: 'equipment', label: 'Equipment', path: '/equipment', icon: Wrench, permission: 'equipment.view', wave: 1 },
  { key: 'maintenance', label: 'Maintenance', path: '/maintenance', icon: Hammer, permission: 'maintenance.view', wave: 1 },
  { key: 'inventory', label: 'Inventory', path: '/inventory', icon: Package, permission: 'inventory.view', wave: 1 },
  { key: 'workouts', label: 'Workouts', path: '/workouts', icon: Activity, permission: 'workouts.view', wave: 1 },
  { key: 'nutrition', label: 'Nutrition', path: '/nutrition', icon: Apple, permission: 'nutrition.view', wave: 1 },
  { key: 'reports', label: 'Reports', path: '/reports', icon: BarChart3, permission: 'reports.view', wave: 1 },
  { key: 'notifications', label: 'Notification Center', path: '/notifications', icon: Bell, permission: 'notifications.view', wave: 1 },
  { key: 'challenges', label: 'Community Challenges', path: '/challenges', icon: Flag, permission: 'experience.manage', wave: 1 },
  { key: 'migration', label: 'Migration Center', path: '/migration', icon: UploadCloud, permission: 'migration.manage', wave: 1 },
  { key: 'settings', label: 'Settings', path: '/settings', icon: Settings, permission: 'settings.view', wave: 1 },
]

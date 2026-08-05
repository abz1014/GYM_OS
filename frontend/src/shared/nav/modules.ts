import type { LucideIcon } from 'lucide-react'
import { Activity, Apple, BarChart3, Bell, CalendarDays, CreditCard, Dumbbell, Flag, Flame, Hammer, LayoutDashboard, NotebookPen, Package, QrCode, Receipt, Settings, Target, UploadCloud, UserCircle, Users, Wrench } from 'lucide-react'

export interface NavModule {
  key: string
  label: string
  path: string
  icon: LucideIcon
  permission: string
  wave: 1 | 2 | 3
}

export const NAV_MODULES: NavModule[] = [
  { key: 'dashboard', label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, permission: 'dashboard.view', wave: 1 },
  // "My Membership", not "My Account": the Topbar avatar menu already owns "My Account" -> /account
  // (password + MFA settings, available to everyone). This sidebar entry is the member's gym home
  // (membership status, today's activity, classes, nutrition), so a distinct name avoids two
  // different "My Account" destinations for a member who sees both at once.
  { key: 'portal', label: 'My Membership', path: '/portal', icon: UserCircle, permission: 'portal.view', wave: 1 },
  // These three (My Membership / My Classes / My Progress) are the member self-service surface, gated
  // on portal.view — the Member role's only permission, and deliberately not granted to any staff
  // role (see DemoDataSeeder), so staff never see dead "link your account" links. "My Classes" is
  // labelled distinctly from the staff "Classes" module below to keep the member-facing page
  // self-evident even though no single user now holds both permissions at once.
  { key: 'log-activity', label: 'Log Activity', path: '/log-activity', icon: NotebookPen, permission: 'portal.view', wave: 1 },
  { key: 'my-classes', label: 'My Classes', path: '/my-classes', icon: CalendarDays, permission: 'portal.view', wave: 1 },
  { key: 'my-progress', label: 'My Progress', path: '/my-progress', icon: Flame, permission: 'portal.view', wave: 1 },
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

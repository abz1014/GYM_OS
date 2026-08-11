import { NAV_MODULES } from '@/shared/nav/modules'

/**
 * Where a specialist's day actually starts.
 *
 * Checked before the dashboard, because `dashboard.view` is seeded to EVERY staff role — so the
 * generic rule was landing a trainer, a nutritionist and a maintenance tech on an executive summary
 * of revenue, expiring memberships and stock levels, none of which is their job. Maureen the trainer
 * opened the app to four figures she could do nothing about, with her actual client list one click
 * away in the sidebar.
 *
 * Each entry names the permission its screen needs as well as the role, so a role stripped of that
 * permission falls through to the generic rules rather than landing on a page that can only 403.
 *
 * Receptionist is deliberately absent: the front desk's home is the member workspace, and that
 * screen does not exist in the right shape yet. Adding an entry here now would only move the same
 * problem to a different page.
 */
const ROLE_HOMES: { role: string; path: string; permission: string }[] = [
  { role: 'Trainer', path: '/my-clients', permission: 'trainers.view' },
  { role: 'Nutritionist', path: '/nutrition', permission: 'nutrition.view' },
  { role: 'Maintenance', path: '/maintenance', permission: 'maintenance.view' },
  { role: 'Accountant', path: '/billing', permission: 'billing.view' },
]

/**
 * Where an authenticated user should land — index route, catch-all, and post-login redirect all
 * need the same answer, so it lives in one place. Specialist home first, then Dashboard (the
 * general staff case), then Portal (the Member role, which holds portal.view and nothing else),
 * then whatever the first permitted nav module is, then /account as a universal fallback that needs
 * no permission at all.
 */
export function resolveLandingRoute(
  hasPermission: (code: string) => boolean,
  roles: readonly string[],
): string {
  // Running the business IS the leadership job, so the executive summary is the right home for it.
  // Checked first so an owner who also holds a Trainer role isn't demoted to the coaching inbox.
  const isLeadership = roles.some((r) => r === 'Owner' || r === 'Manager')

  if (!isLeadership) {
    const home = ROLE_HOMES.find((h) => roles.includes(h.role) && hasPermission(h.permission))
    if (home) {
      return home.path
    }
  }

  if (hasPermission('dashboard.view')) {
    return '/dashboard'
  }

  if (hasPermission('portal.view')) {
    return '/portal'
  }

  // No wave filter any more: every module ships, and NAV_MODULES is ordered by section, so the first
  // permitted entry is already the most prominent thing this user can actually open.
  const firstAllowed = NAV_MODULES.find((m) => hasPermission(m.permission))
  return firstAllowed?.path ?? '/account'
}

import { NAV_MODULES } from '@/shared/nav/modules'

/**
 * Where an authenticated user should land — index route, catch-all, and post-login redirect all
 * need the same answer, so it lives in one place. Dashboard first (the common staff case), then
 * Portal (the Member role, which holds portal.view and nothing else), then whatever the first
 * permitted nav module is, then /account as a universal fallback that needs no permission at all.
 */
export function resolveLandingRoute(hasPermission: (code: string) => boolean): string {
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

import { Outlet, useLocation } from 'react-router-dom'
import { Lock } from 'lucide-react'

import { NAV_MODULES } from '@/shared/nav/modules'
import { useAuthStore } from '@/stores/authStore'

/**
 * The route-level half of permission gating.
 *
 * RequireAuth checks for a token and nothing else, so every staff route was reachable by typing its
 * URL. The nav hid the entries, which is not the same thing as refusing them: a Nutritionist who
 * typed /workouts got the Workouts page, whose every query then 403'd, and the screen rendered as a
 * fully-shaped module containing nothing. "You cannot see this" and "there is nothing here" are
 * different sentences and only one of them was true.
 *
 * The rule is read from NAV_MODULES rather than restated here, because a second list of which
 * permission guards which path is a second thing to keep in step with the first. A path with no
 * entry in that list is deliberately allowed through — /account, /portal and the member screens are
 * not modules and have no permission of their own.
 *
 * This is a usability guard, not a security boundary. Every endpoint behind these screens enforces
 * its own permission server-side and returns 403 regardless of what the browser lets you open;
 * removing this component would make the app ruder, not less safe.
 */
export function RequireModuleAccess() {
  const { pathname } = useLocation()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const roles = useAuthStore((s) => s.user)?.roles ?? []

  // Longest match wins, so /members/:id is governed by /members rather than by a shorter prefix
  // that happens to also match. Comparing on a segment boundary keeps /my-clients from being
  // treated as a child of a hypothetical /my.
  const active = NAV_MODULES.filter((m) => pathname === m.path || pathname.startsWith(`${m.path}/`)).sort(
    (a, b) => b.path.length - a.path.length,
  )[0]

  if (!active) return <Outlet />

  const permitted = hasPermission(active.permission) && (!active.requiresRole || roles.includes(active.requiresRole))

  if (permitted) return <Outlet />

  return (
    <div className="flex flex-col items-center gap-3 px-4 py-20 text-center">
      <Lock className="size-8 text-muted-foreground" aria-hidden />
      <h1 className="font-display text-2xl font-black tracking-tight">{active.label} isn't open to your role</h1>
      {/* Names the thing that is missing rather than apologising vaguely — the person reading this
          usually needs to ask someone for it, and cannot ask for what they cannot name. */}
      <p className="max-w-md text-sm text-muted-foreground">
        This screen needs the <span className="font-mono text-foreground">{active.permission}</span> permission
        {active.requiresRole ? ` and the ${active.requiresRole} role` : ''}. Ask an administrator if you
        need access.
      </p>
    </div>
  )
}

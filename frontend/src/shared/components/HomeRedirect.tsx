import { Navigate } from 'react-router-dom'

import { useAuthStore } from '@/stores/authStore'
import { resolveLandingRoute } from '@/shared/nav/landingRoute'

/** Sends an authenticated user to the first screen their permissions actually allow, instead of
 * a hardcoded /dashboard that 403s for roles (e.g. Member) that don't hold dashboard.view. */
export function HomeRedirect() {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const roles = useAuthStore((s) => s.user?.roles)
  return <Navigate to={resolveLandingRoute(hasPermission, roles ?? [])} replace />
}

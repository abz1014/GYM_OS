import { useEffect } from 'react'

import { apiClient } from '@/lib/apiClient'
import { useAuthStore } from '@/stores/authStore'
import type { CurrentUser } from '@/types/auth'

/**
 * Keeps the cached permission list honest against the server.
 *
 * THE DEFECT. `user.permissions` is persisted to localStorage at LOGIN and replaced only by another
 * login, while the sidebar and the route guard read from that cache. The backend resolves
 * permissions per request, so revoking access took effect immediately on the API and not at all in
 * the employee's open tab: an owner revoking a departing employee watched the matrix update and
 * reasonably believed access was gone, while that employee kept their full navigation until they
 * chose to log out. The two sides disagreed for as long as the tab stayed open.
 *
 * Two triggers, both cheap:
 *  - returning to the tab, which is when a person who has been away resumes acting;
 *  - a 403 from any request, raised by apiClient's interceptor — the server just told us the cache
 *    is wrong, which is the strongest possible signal to re-read it.
 *
 * This narrows the window rather than closing it, and that is the honest claim: the API is the
 * enforcement boundary and always was. What this fixes is a UI that lies about what someone can do.
 * Deliberately NOT a poll — an interval would spend a request every N seconds on every open tab to
 * catch an event that happens a few times a year.
 */
export function usePermissionSync() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const setUser = useAuthStore((s) => s.setUser)

  useEffect(() => {
    if (!accessToken) return

    let cancelled = false

    const resync = async () => {
      try {
        const { data } = await apiClient.get<CurrentUser>('/api/auth/me')
        // A resync landing after logout must not resurrect a session.
        if (!cancelled && useAuthStore.getState().accessToken) setUser(data)
      } catch {
        // 401 is already handled by the interceptor (it clears the session); anything else means the
        // network is unhappy, and guessing at permissions from a failed request would be worse than
        // keeping what we have until the next attempt.
      }
    }

    const onFocus = () => void resync()
    window.addEventListener('focus', onFocus)
    window.addEventListener('gymos:permission-denied', onFocus)

    return () => {
      cancelled = true
      window.removeEventListener('focus', onFocus)
      window.removeEventListener('gymos:permission-denied', onFocus)
    }
  }, [accessToken, setUser])
}

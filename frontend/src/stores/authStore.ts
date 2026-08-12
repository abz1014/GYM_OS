import { create } from 'zustand'
import { persist } from 'zustand/middleware'

import { queryClient } from '@/lib/queryClient'
import type { AuthResult, CurrentUser } from '@/types/auth'

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  user: CurrentUser | null
  setSession: (result: AuthResult) => void
  setUser: (user: CurrentUser) => void
  clearSession: () => void
  hasPermission: (code: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      setSession: (result) =>
        set({
          accessToken: result.accessToken,
          refreshToken: result.refreshToken,
          user: result.user,
        }),
      setUser: (user) => set({ user }),
      /*
       * The query cache goes with the session, always.
       *
       * Nothing here used to touch it, and no query key carries a user identity — ['members', …],
       * ['dashboard-summary', branchId], ['dashboard-overdue-invoices'] and the rest are keyed by
       * what was asked, never by who asked. With a 30s staleTime and the default five-minute gcTime,
       * logging out as the owner and back in as a receptionist in the same tab was a key-for-key hit:
       * React Query served the owner's rows synchronously, isSuccess true, no request, no error. A
       * screen that has never made a request cannot detect that it is showing someone else's data.
       *
       * Done in clearSession rather than at the logout button because there are three ways out — the
       * staff sidebar, the member portal's More page, and apiClient's 401 interceptor — and the one
       * that matters most is the interceptor, which fires when a token expires with nobody watching.
       */
      clearSession: () => {
        queryClient.clear()
        set({ accessToken: null, refreshToken: null, user: null })
      },
      hasPermission: (code) => get().user?.permissions.includes(code) ?? false,
    }),
    { name: 'gymos-auth' }
  )
)

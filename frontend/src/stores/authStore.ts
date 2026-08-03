import { create } from 'zustand'
import { persist } from 'zustand/middleware'

import type { AuthResult, CurrentUser } from '@/types/auth'

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  user: CurrentUser | null
  setSession: (result: AuthResult) => void
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
      clearSession: () => set({ accessToken: null, refreshToken: null, user: null }),
      hasPermission: (code) => get().user?.permissions.includes(code) ?? false,
    }),
    { name: 'gymos-auth' }
  )
)

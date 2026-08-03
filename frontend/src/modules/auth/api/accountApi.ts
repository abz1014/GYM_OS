import { useMutation, useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import { useAuthStore } from '@/stores/authStore'
import type { CurrentUser } from '@/types/auth'

export function useCurrentUser() {
  return useQuery({
    queryKey: ['current-user'],
    queryFn: async () => (await apiClient.get<CurrentUser>('/api/auth/me')).data,
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (input: { currentPassword: string; newPassword: string }) =>
      apiClient.post('/api/auth/change-password', input),
  })
}

export interface MfaSetupResult {
  secret: string
  qrCodeUri: string
}

export function useInitiateMfaSetup() {
  return useMutation({
    mutationFn: async () => (await apiClient.post<MfaSetupResult>('/api/auth/mfa/initiate')).data,
  })
}

export function useConfirmMfaSetup() {
  const setUser = useAuthStore((s) => s.setUser)
  return useMutation({
    mutationFn: async (code: string) => apiClient.post('/api/auth/mfa/confirm', { code }),
    onSuccess: async () => {
      const { data } = await apiClient.get<CurrentUser>('/api/auth/me')
      setUser(data)
    },
  })
}

export function useDisableMfa() {
  const setUser = useAuthStore((s) => s.setUser)
  return useMutation({
    mutationFn: async (password: string) => apiClient.post('/api/auth/mfa/disable', { password }),
    onSuccess: async () => {
      const { data } = await apiClient.get<CurrentUser>('/api/auth/me')
      setUser(data)
    },
  })
}

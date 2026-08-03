import { useMutation } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { AuthResult } from '@/types/auth'

interface LoginInput {
  email: string
  password: string
  mfaCode?: string | null
}

export function useLogin() {
  return useMutation({
    mutationFn: async (input: LoginInput) => (await apiClient.post<AuthResult>('/api/auth/login', input)).data,
  })
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: async (email: string) => apiClient.post('/api/auth/forgot-password', { email }),
  })
}

export function useLogout() {
  return useMutation({
    mutationFn: async (refreshToken: string) => apiClient.post('/api/auth/logout', { refreshToken }),
  })
}

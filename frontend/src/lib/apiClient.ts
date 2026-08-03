import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'

import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import type { AuthResult } from '@/types/auth'

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000',
})

apiClient.interceptors.request.use((config) => {
  const { accessToken } = useAuthStore.getState()
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`)
  }

  const branchId = useUiStore.getState().selectedBranchId
  if (branchId) {
    config.headers.set('X-Branch-Id', branchId)
  }

  return config
})

let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken, setSession, clearSession } = useAuthStore.getState()
  if (!refreshToken) {
    return null
  }

  try {
    const response = await axios.post<AuthResult>(
      `${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'}/api/auth/refresh-token`,
      { refreshToken }
    )
    setSession(response.data)
    return response.data.accessToken
  } catch {
    clearSession()
    return null
  }
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined

    // A 401 from the login endpoint itself means "bad credentials" or "MFA code required" —
    // normal application-level failures the login form needs to handle, not an expired session.
    // Treating it as one caused a redirect-to-/login that reloaded the page before the form's own
    // error handling ever ran, wiping out any in-progress MFA prompt.
    const isLoginRequest = originalRequest?.url?.includes('/api/auth/login')

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry && !isLoginRequest) {
      originalRequest._retry = true

      refreshPromise ??= refreshAccessToken().finally(() => {
        refreshPromise = null
      })

      const newToken = await refreshPromise
      if (newToken) {
        originalRequest.headers.set('Authorization', `Bearer ${newToken}`)
        return apiClient(originalRequest)
      }

      window.location.assign('/login')
    }

    return Promise.reject(error)
  }
)

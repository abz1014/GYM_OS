import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface GymProfile {
  legalName: string
  displayName: string
  logoUrl: string | null
  supportEmail: string | null
  supportPhone: string | null
  defaultCurrency: string
  defaultTimeZone: string
}

export function useGymProfile() {
  return useQuery({
    queryKey: ['gym-profile'],
    queryFn: async () => (await apiClient.get<GymProfile>('/api/settings/gym-profile')).data,
  })
}

export function useUpdateGymProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: GymProfile) => apiClient.put('/api/settings/gym-profile', input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['gym-profile'] }),
  })
}

export interface Branch {
  id: string
  name: string
  addressLine: string
  city: string
  country: string
  timeZone: string
  currency: string
  isActive: boolean
}

export function useAdminBranchesList(includeInactive = true) {
  return useQuery({
    queryKey: ['admin-branches', includeInactive],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches', { params: { includeInactive } })).data,
  })
}

interface CreateBranchInput {
  name: string
  addressLine: string
  city: string
  country: string
  timeZone: string
  currency: string
}

export function useCreateBranch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateBranchInput) => (await apiClient.post<string>('/api/branches', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-branches'] }),
  })
}

interface UpdateBranchInput extends CreateBranchInput {
  id: string
  isActive: boolean
}

export function useUpdateBranch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateBranchInput) => apiClient.put(`/api/branches/${input.id}`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-branches'] }),
  })
}

export interface Role {
  id: string
  name: string
}

export interface PermissionCatalogEntry {
  id: string
  code: string
  module: string
  description: string
}

export interface RolePermissionGrant {
  roleId: string
  permissionId: string
}

export interface PermissionMatrix {
  roles: Role[]
  permissions: PermissionCatalogEntry[]
  grants: RolePermissionGrant[]
}

export function usePermissionMatrix() {
  return useQuery({
    queryKey: ['permission-matrix'],
    queryFn: async () => (await apiClient.get<PermissionMatrix>('/api/settings/permission-matrix')).data,
  })
}

export function useSetRolePermission() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { roleId: string; permissionId: string; granted: boolean }) =>
      apiClient.put('/api/settings/permission-matrix', input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['permission-matrix'] }),
  })
}

export interface SystemPreference {
  id: string
  branchId: string | null
  key: string
  value: string
  description: string | null
}

export function useSystemPreferences(branchId?: string | null) {
  return useQuery({
    queryKey: ['system-preferences', branchId],
    queryFn: async () => (await apiClient.get<SystemPreference[]>('/api/settings/system-preferences', { params: { branchId } })).data,
  })
}

interface UpsertSystemPreferenceInput {
  branchId?: string
  key: string
  value: string
  description?: string
}

export function useUpsertSystemPreference() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpsertSystemPreferenceInput) =>
      (await apiClient.put<string>('/api/settings/system-preferences', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['system-preferences'] }),
  })
}

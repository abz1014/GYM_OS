import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

/**
 * Staff accounts — hiring, firing, and everything between.
 *
 * THE GAP THIS FILLS. There was no screen, endpoint or permission for creating or deactivating a
 * staff account anywhere in the product: every account that existed had been made by the demo
 * seeder, and the only user the app could create was a trainer (who then could never be edited or
 * deactivated either). Hiring a receptionist meant a developer running a CLI seed, while the
 * console cheerfully told blocked users to "ask an administrator" — a person with no screen to act
 * on it.
 *
 * Roles ride along on the list response rather than coming from the permission matrix: the matrix
 * is gated on settings.manage_permissions, and a manager who may hire staff is not necessarily
 * someone who may rewrite the permission grid. One request, one permission, no coupling.
 */

export interface StaffMember {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  isActive: boolean
  roleName: string
  branchIds: string[]
  lastLoginAt: string | null
}

export interface StaffRole {
  id: string
  name: string
}

export interface StaffDirectory {
  staff: StaffMember[]
  roles: StaffRole[]
}

export interface StaffInput {
  firstName: string
  lastName: string
  phone: string | null
  roleName: string
  branchIds: string[]
}

/** The one-time password to read out to the new hire. Never stored — see CreateStaffCommand. */
export interface TemporaryPasswordResult {
  temporaryPassword: string
}

const STAFF_KEY = ['settings', 'staff']

export function useStaffDirectory() {
  return useQuery({
    queryKey: STAFF_KEY,
    queryFn: async () => (await apiClient.get<StaffDirectory>('/api/settings/staff')).data,
  })
}

function useStaffMutation<TVariables, TResult>(fn: (vars: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: STAFF_KEY }),
  })
}

export function useCreateStaff() {
  return useStaffMutation(async (input: StaffInput & { email: string }) =>
    (await apiClient.post<{ id: string } & TemporaryPasswordResult>('/api/settings/staff', input)).data,
  )
}

export function useUpdateStaff() {
  return useStaffMutation(async ({ id, ...input }: StaffInput & { id: string }) =>
    apiClient.put(`/api/settings/staff/${id}`, input),
  )
}

export function useSetStaffActive() {
  return useStaffMutation(async ({ id, isActive }: { id: string; isActive: boolean }) =>
    apiClient.post(`/api/settings/staff/${id}/${isActive ? 'reactivate' : 'deactivate'}`),
  )
}

export function useResetStaffPassword() {
  return useStaffMutation(async (id: string) =>
    (await apiClient.post<TemporaryPasswordResult>(`/api/settings/staff/${id}/reset-password`)).data,
  )
}

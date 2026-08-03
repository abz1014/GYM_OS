import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type MemberStatus = 'Active' | 'Frozen' | 'Expired' | 'Cancelled'

export interface MemberListItem {
  id: string
  memberCode: string
  fullName: string
  email: string
  phone: string | null
  profilePhotoUrl: string | null
  status: MemberStatus
  joinDate: string
}

export interface MemberMembership {
  id: string
  membershipPlanId: string
  membershipPlanName: string
  startDate: string
  endDate: string
  status: string
  autoRenew: boolean
  freezeStartDate: string | null
  freezeEndDate: string | null
  pricePaid: number
  currency: string
}

export interface MemberDetail extends MemberListItem {
  firstName: string
  lastName: string
  dateOfBirth: string | null
  gender: string | null
  address: string | null
  qrCodeToken: string
  branchId: string
  emergencyContacts: { id: string; name: string; relationship: string; phone: string; email: string | null }[]
  medicalNotes: { id: string; note: string; recordedAt: string }[]
  measurements: { id: string; measuredOn: string; weightKg: number | null; bodyFatPercentage: number | null }[]
  progressPhotos: { id: string; photoUrl: string; takenAt: string }[]
  memberMemberships: MemberMembership[]
}

interface ListParams {
  searchTerm?: string
  status?: MemberStatus
  branchId?: string | null
  page?: number
  pageSize?: number
}

export function useMembersList(params: ListParams) {
  return useQuery({
    queryKey: ['members', params],
    queryFn: async () => (await apiClient.get<PagedList<MemberListItem>>('/api/members', { params })).data,
  })
}

export function useMember(id: string | undefined) {
  return useQuery({
    queryKey: ['member', id],
    queryFn: async () => (await apiClient.get<MemberDetail>(`/api/members/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateMemberInput {
  firstName: string
  lastName: string
  email: string
  phone?: string
  branchId: string
}

export function useCreateMember() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateMemberInput) => (await apiClient.post<string>('/api/members', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['members'] }),
  })
}

export function useRenewMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { membershipPlanId: string; startDate: string; autoRenew: boolean; couponCode?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/memberships`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useFreezeMembership(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberMembershipId: string; freezeStartDate: string; freezeEndDate: string }) =>
      apiClient.post(`/api/members/memberships/${input.memberMembershipId}/freeze`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useTransferMember(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { newBranchId: string }) => apiClient.post(`/api/members/${memberId}/transfer`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['member', memberId] })
      queryClient.invalidateQueries({ queryKey: ['members'] })
    },
  })
}

interface UpdateMemberInput {
  firstName: string
  lastName: string
  email: string
  phone?: string | null
  dateOfBirth?: string | null
  gender?: string | null
  address?: string | null
  profilePhotoUrl?: string | null
}

export function useUpdateMember(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateMemberInput) => apiClient.put(`/api/members/${memberId}`, { id: memberId, ...input }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['member', memberId] })
      queryClient.invalidateQueries({ queryKey: ['members'] })
    },
  })
}

export function useAddEmergencyContact(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { name: string; relationship: string; phone: string; email?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/emergency-contacts`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useAddMedicalNote(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { note: string; recordedByUserId?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/medical-notes`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

interface AddMeasurementInput {
  measuredOn: string
  weightKg?: number | null
  bodyFatPercentage?: number | null
  chestCm?: number | null
  waistCm?: number | null
  hipCm?: number | null
  armCm?: number | null
  thighCm?: number | null
  notes?: string | null
}

export function useAddMeasurement(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AddMeasurementInput) =>
      (await apiClient.post(`/api/members/${memberId}/measurements`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

export function useAddProgressPhoto(memberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { photoUrl: string; notes?: string | null }) =>
      (await apiClient.post(`/api/members/${memberId}/progress-photos`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['member', memberId] }),
  })
}

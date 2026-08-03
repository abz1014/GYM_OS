import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export type LeadSource = 'WalkIn' | 'Referral' | 'SocialMedia' | 'Website' | 'Advertisement' | 'Other'
export type LeadStage = 'Lead' | 'FollowUp' | 'Trial' | 'Member' | 'Lost'
export type LeadActivityType = 'Call' | 'Email' | 'Meeting' | 'Note' | 'TrialScheduled'

export interface LeadListItem {
  id: string
  fullName: string
  email: string
  phone: string | null
  source: LeadSource
  stage: LeadStage
  assignedToUserId: string | null
  createdAt: string
}

export interface LeadActivityItem {
  id: string
  type: LeadActivityType
  notes: string
  dueDate: string | null
  completedAt: string | null
}

export interface LeadDetail {
  id: string
  firstName: string
  lastName: string
  email: string
  phone: string | null
  source: LeadSource
  stage: LeadStage
  branchId: string
  assignedToUserId: string | null
  convertedMemberId: string | null
  notes: string | null
  createdAt: string
  activities: LeadActivityItem[]
}

export interface CrmPipelineSummary {
  leadCount: number
  followUpCount: number
  trialCount: number
  memberCount: number
  lostCount: number
  conversionRatePercent: number
}

export const PIPELINE_STAGES: LeadStage[] = ['Lead', 'FollowUp', 'Trial', 'Member', 'Lost']

export function useLeadsList(params: { stage?: LeadStage; branchId?: string | null }) {
  return useQuery({
    queryKey: ['leads', params],
    queryFn: async () => (await apiClient.get<LeadListItem[]>('/api/leads', { params })).data,
  })
}

export function useCrmPipelineSummary(branchId?: string | null) {
  return useQuery({
    queryKey: ['crm-summary', branchId],
    queryFn: async () => (await apiClient.get<CrmPipelineSummary>('/api/leads/summary', { params: { branchId } })).data,
    enabled: !!branchId,
  })
}

export function useLead(id: string | undefined) {
  return useQuery({
    queryKey: ['lead', id],
    queryFn: async () => (await apiClient.get<LeadDetail>(`/api/leads/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateLeadInput {
  firstName: string
  lastName: string
  email: string
  phone?: string
  source: LeadSource
  branchId: string
}

export function useCreateLead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateLeadInput) => (await apiClient.post<string>('/api/leads', input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] })
      queryClient.invalidateQueries({ queryKey: ['crm-summary'] })
    },
  })
}

export function useUpdateLeadStage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { leadId: string; stage: LeadStage }) =>
      apiClient.put(`/api/leads/${input.leadId}/stage`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] })
      queryClient.invalidateQueries({ queryKey: ['crm-summary'] })
      queryClient.invalidateQueries({ queryKey: ['lead'] })
    },
  })
}

export function useAddLeadActivity(leadId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { type: LeadActivityType; notes: string; dueDate?: string | null }) =>
      (await apiClient.post(`/api/leads/${leadId}/activities`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['lead', leadId] }),
  })
}

export function useCompleteLeadActivity(leadId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (activityId: string) => apiClient.post(`/api/leads/activities/${activityId}/complete`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['lead', leadId] }),
  })
}

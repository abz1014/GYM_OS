import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export type MembershipPlanType = 'Monthly' | 'Quarterly' | 'Annual' | 'Family' | 'Corporate' | 'Custom'

export interface MembershipPlan {
  id: string
  name: string
  type: MembershipPlanType
  description: string | null
  durationDays: number
  price: number
  currency: string
  maxFreezeDays: number
  isActive: boolean
}

export function useMembershipPlans(includeInactive = false) {
  return useQuery({
    queryKey: ['membership-plans', includeInactive],
    queryFn: async () => (await apiClient.get<MembershipPlan[]>('/api/membership-plans', { params: { includeInactive } })).data,
  })
}

interface CreatePlanInput {
  name: string
  type: MembershipPlanType
  description?: string
  durationDays: number
  price: number
  currency: string
  maxFreezeDays: number
}

export function useCreateMembershipPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreatePlanInput) => (await apiClient.post('/api/membership-plans', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['membership-plans'] }),
  })
}

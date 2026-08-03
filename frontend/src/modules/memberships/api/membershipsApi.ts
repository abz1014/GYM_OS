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

export type DiscountType = 'Percentage' | 'FixedAmount'

export interface Discount {
  id: string
  name: string
  type: DiscountType
  value: number
  membershipPlanId: string | null
  validFrom: string | null
  validTo: string | null
  isActive: boolean
}

export interface Coupon {
  id: string
  code: string
  discountId: string
  maxRedemptions: number | null
  timesRedeemed: number
  validFrom: string | null
  validTo: string | null
  isActive: boolean
}

export function useDiscounts(includeInactive = false) {
  return useQuery({
    queryKey: ['discounts', includeInactive],
    queryFn: async () => (await apiClient.get<Discount[]>('/api/membership-plans/discounts', { params: { includeInactive } })).data,
  })
}

interface CreateDiscountInput {
  name: string
  type: DiscountType
  value: number
  membershipPlanId?: string
  validFrom?: string
  validTo?: string
}

export function useCreateDiscount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateDiscountInput) => (await apiClient.post<string>('/api/membership-plans/discounts', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['discounts'] }),
  })
}

export function useCoupons(includeInactive = false) {
  return useQuery({
    queryKey: ['coupons', includeInactive],
    queryFn: async () => (await apiClient.get<Coupon[]>('/api/membership-plans/coupons', { params: { includeInactive } })).data,
  })
}

interface CreateCouponInput {
  code: string
  discountId: string
  maxRedemptions?: number
  validFrom?: string
  validTo?: string
}

export function useCreateCoupon() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateCouponInput) => (await apiClient.post<string>('/api/membership-plans/coupons', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['coupons'] }),
  })
}

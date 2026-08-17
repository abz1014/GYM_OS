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

/**
 * The kill switch discounts and coupons never had.
 *
 * Both entities have carried an `IsActive` flag since they were created — the redemption path reads
 * it (`Coupon.IsRedeemable`) and the list screen already dims an inactive row. What was missing was
 * any way to SET it: create was the only verb either had, so a code posted publicly, mispriced, or
 * simply past its usefulness stayed live for good. A 100%-off code with no cap is unbounded revenue
 * leakage, and the only remedy was a developer with database access.
 *
 * Reversible on purpose. Deleting would orphan the discount rows on invoices that already redeemed
 * it and destroy the record of why someone paid less; switching it off stops all future redemptions
 * and keeps the history intact.
 */
export function useSetCouponActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) =>
      apiClient.post(`/api/membership-plans/coupons/${id}/set-active`, { isActive }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['coupons'] }),
  })
}

export function useSetDiscountActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) =>
      apiClient.post(`/api/membership-plans/discounts/${id}/set-active`, { isActive }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['discounts'] }),
  })
}

/**
 * Editing a plan — price, name, freeze allowance, and whether it is still sold.
 *
 * `PUT /api/membership-plans/{id}` and UpdateMembershipPlanCommand have existed all along; the
 * frontend only ever shipped CreatePlanDialog, so raising a price or retiring a plan was impossible
 * from the console and required hitting the API by hand. Retiring is `isActive: false` rather than a
 * delete, because members already on the plan keep a valid reference to it — deleting would orphan
 * live memberships and every invoice that ever cited it.
 */
export function useUpdateMembershipPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      id: string
      name: string
      description: string | null
      price: number
      maxFreezeDays: number
      isActive: boolean
    }) => apiClient.put(`/api/membership-plans/${input.id}`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['membership-plans'] }),
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface FoodItem {
  id: string
  name: string
  caloriesPerServing: number
  proteinG: number
  carbsG: number
  fatG: number
  servingSizeDescription: string
}

export function useFoodItemsList() {
  return useQuery({
    queryKey: ['food-items'],
    queryFn: async () => (await apiClient.get<FoodItem[]>('/api/nutrition/food-items')).data,
  })
}

export function useCreateFoodItem() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      name: string
      caloriesPerServing: number
      proteinG: number
      carbsG: number
      fatG: number
      servingSizeDescription: string
    }) => (await apiClient.post<string>('/api/nutrition/food-items', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['food-items'] }),
  })
}

export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'

export interface DietPlanListItem {
  id: string
  name: string
  targetCalories: number | null
  targetProteinG: number | null
  targetCarbsG: number | null
  targetFatG: number | null
  startDate: string
  endDate: string | null
}

export interface MealEntry {
  id: string
  foodItemId: string
  foodItemName: string
  mealType: MealType
  quantity: number
  consumedAt: string | null
}

export interface DietPlanDetail {
  id: string
  memberId: string
  name: string
  targetCalories: number | null
  targetProteinG: number | null
  targetCarbsG: number | null
  targetFatG: number | null
  startDate: string
  endDate: string | null
  mealEntries: MealEntry[]
}

export interface WaterLog {
  id: string
  amountMl: number
  loggedAt: string
}

export interface NutritionClientRow {
  memberId: string
  memberName: string
  memberCode: string
  planName: string
  startDate: string
  endDate: string | null
  /** False once the plan's end date has passed — the row stays, because the member has not. */
  planIsActive: boolean
  lastMealLoggedAt: string | null
  mealsLoggedLast7Days: number
}

export interface MyNutritionClients {
  clients: NutritionClientRow[]
  activeMembersWithoutAPlan: number
}

/**
 * Who this nutritionist is responsible for, derived from the plans they wrote. The trainer has had a
 * roster since the coaching inbox landed; this is the same idea for the other specialist.
 */
export function useMyNutritionClients() {
  return useQuery({
    queryKey: ['nutrition-my-clients'],
    queryFn: async () => (await apiClient.get<MyNutritionClients>('/api/nutrition/my-clients')).data,
  })
}

export function useMemberDietPlans(memberId: string | undefined) {
  return useQuery({
    queryKey: ['diet-plans', memberId],
    queryFn: async () => (await apiClient.get<DietPlanListItem[]>(`/api/nutrition/diet-plans/member/${memberId}`)).data,
    enabled: !!memberId,
  })
}

export function useDietPlan(id: string | undefined) {
  return useQuery({
    queryKey: ['diet-plan', id],
    queryFn: async () => (await apiClient.get<DietPlanDetail>(`/api/nutrition/diet-plans/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateDietPlanInput {
  memberId: string
  name: string
  targetCalories?: number
  targetProteinG?: number
  targetCarbsG?: number
  targetFatG?: number
  startDate: string
  endDate?: string
}

export function useCreateDietPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateDietPlanInput) => (await apiClient.post<string>('/api/nutrition/diet-plans', input)).data,
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['diet-plans', variables.memberId] })
      // Writing a plan is what puts a member on the roster, so the roster is now wrong.
      queryClient.invalidateQueries({ queryKey: ['nutrition-my-clients'] })
    },
  })
}

interface AddMealEntryInput {
  foodItemId: string
  mealType: MealType
  quantity: number
}

export function useAddMealEntry(dietPlanId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AddMealEntryInput) => (await apiClient.post<string>(`/api/nutrition/diet-plans/${dietPlanId}/meals`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['diet-plan', dietPlanId] }),
  })
}

export function useMemberWaterLogs(memberId: string | undefined) {
  return useQuery({
    queryKey: ['water-logs', memberId],
    queryFn: async () => (await apiClient.get<WaterLog[]>(`/api/nutrition/water/member/${memberId}`)).data,
    enabled: !!memberId,
  })
}

export function useLogWater() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberId: string; amountMl: number }) => (await apiClient.post<string>('/api/nutrition/water', input)).data,
    onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: ['water-logs', variables.memberId] }),
  })
}

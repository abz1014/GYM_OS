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
  startDate: string
  endDate: string | null
  mealEntries: MealEntry[]
}

export interface WaterLog {
  id: string
  amountMl: number
  loggedAt: string
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
  startDate: string
  endDate?: string
}

export function useCreateDietPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateDietPlanInput) => (await apiClient.post<string>('/api/nutrition/diet-plans', input)).data,
    onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: ['diet-plans', variables.memberId] }),
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

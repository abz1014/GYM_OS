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

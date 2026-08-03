import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export type InventoryCategory = 'Supplement' | 'Merchandise' | 'CleaningSupply' | 'SparePart'

export interface InventoryItemListItem {
  id: string
  sku: string
  name: string
  category: InventoryCategory
  quantityOnHand: number
  reorderLevel: number
  isLowStock: boolean
  unitPrice: number | null
}

export function useInventoryItemsList(params: { branchId?: string | null; category?: InventoryCategory; lowStockOnly?: boolean }) {
  return useQuery({
    queryKey: ['inventory-items', params],
    queryFn: async () => (await apiClient.get<InventoryItemListItem[]>('/api/inventory', { params })).data,
  })
}

interface CreateInventoryItemInput {
  sku: string
  name: string
  category: InventoryCategory
  branchId: string
  quantityOnHand: number
  reorderLevel: number
  unitCost?: number
  unitPrice?: number
}

export function useCreateInventoryItem() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateInventoryItemInput) => (await apiClient.post<string>('/api/inventory', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-items'] }),
  })
}

export function useRecordStockMovement(itemId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { type: 'In' | 'Out'; quantity: number; reason?: string }) =>
      apiClient.post(`/api/inventory/${itemId}/movements`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-items'] }),
  })
}

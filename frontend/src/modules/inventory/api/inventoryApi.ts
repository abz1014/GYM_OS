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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inventory-items'] })
      queryClient.invalidateQueries({ queryKey: ['inventory-item', itemId] })
    },
  })
}

export interface StockMovementListItem {
  id: string
  type: 'In' | 'Out'
  quantity: number
  reason: string | null
  movedAt: string
}

export interface PurchaseRecordListItem {
  id: string
  supplierName: string | null
  quantity: number
  unitCost: number
  purchasedAt: string
  invoiceReference: string | null
}

export interface InventoryItemDetail {
  id: string
  sku: string
  name: string
  category: InventoryCategory
  quantityOnHand: number
  reorderLevel: number
  isLowStock: boolean
  unitCost: number | null
  unitPrice: number | null
  branchId: string
  stockMovements: StockMovementListItem[]
  purchaseRecords: PurchaseRecordListItem[]
}

export function useInventoryItem(id: string | undefined) {
  return useQuery({
    queryKey: ['inventory-item', id],
    queryFn: async () => (await apiClient.get<InventoryItemDetail>(`/api/inventory/${id}`)).data,
    enabled: !!id,
  })
}

interface RecordPurchaseInput {
  supplierId?: string
  quantity: number
  unitCost: number
  invoiceReference?: string
}

export function useRecordPurchase(itemId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: RecordPurchaseInput) => (await apiClient.post<string>(`/api/inventory/${itemId}/purchases`, input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inventory-items'] })
      queryClient.invalidateQueries({ queryKey: ['inventory-item', itemId] })
    },
  })
}

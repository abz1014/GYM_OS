import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type AssetStatus = 'Active' | 'UnderMaintenance' | 'OutOfService' | 'Retired'

export interface AssetListItem {
  id: string
  assetTag: string
  name: string
  category: string | null
  status: AssetStatus
  warrantyExpiresAt: string | null
  supplierName: string | null
}

export interface AssetDetail {
  id: string
  assetTag: string
  name: string
  category: string | null
  qrCodeToken: string
  photoUrls: string[]
  manualUrl: string | null
  warrantyExpiresAt: string | null
  supplierId: string | null
  supplierName: string | null
  status: AssetStatus
  purchaseDate: string | null
  purchasePrice: number | null
  notes: string | null
  branchId: string
}

export interface Supplier {
  id: string
  name: string
  contactName: string | null
  phone: string | null
  email: string | null
}

export function useAssetsList(params: {
  branchId?: string | null
  status?: AssetStatus
  category?: string
  page?: number
  pageSize?: number
}) {
  return useQuery({
    queryKey: ['assets', params],
    queryFn: async () => (await apiClient.get<PagedList<AssetListItem>>('/api/equipment', { params })).data,
  })
}

export function useAsset(id: string | undefined) {
  return useQuery({
    queryKey: ['asset', id],
    queryFn: async () => (await apiClient.get<AssetDetail>(`/api/equipment/${id}`)).data,
    enabled: !!id,
  })
}

export function useSuppliers() {
  return useQuery({
    queryKey: ['suppliers'],
    queryFn: async () => (await apiClient.get<Supplier[]>('/api/equipment/suppliers')).data,
  })
}

interface CreateAssetInput {
  name: string
  category?: string
  branchId: string
  supplierId?: string
  purchasePrice?: number
}

export function useCreateAsset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateAssetInput) => (await apiClient.post<string>('/api/equipment', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['assets'] }),
  })
}

interface CreateSupplierInput {
  name: string
  contactName?: string
  phone?: string
  email?: string
  address?: string
}

export function useCreateSupplier() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateSupplierInput) => (await apiClient.post<string>('/api/equipment/suppliers', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['suppliers'] }),
  })
}

/**
 * The one mutation in this app that a person fires without pressing a button — it hangs off a
 * `<Select>` whose value is server data, so a rejected write simply snaps the dropdown back to what
 * it was. There is no MutationCache onError in lib/queryClient.ts, which means nothing else in the
 * app would say a word: the toast below is the only thing standing between a failed write and
 * someone walking away believing the asset is now out of service.
 */
export function useUpdateAssetStatus(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (status: AssetStatus) => apiClient.put(`/api/equipment/${assetId}/status`, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['asset', assetId] })
      queryClient.invalidateQueries({ queryKey: ['assets'] })
    },
    onError: () => toast.error('Could not update the asset status.'),
  })
}

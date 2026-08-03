import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export type WorkOrderType = 'Preventive' | 'Corrective'
export type WorkOrderPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type WorkOrderStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled'

export interface WorkOrderListItem {
  id: string
  assetName: string
  assetTag: string
  type: WorkOrderType
  priority: WorkOrderPriority
  status: WorkOrderStatus
  title: string
  scheduledDate: string | null
  isOverdue: boolean
}

export function useWorkOrdersList(params: { branchId?: string | null; status?: WorkOrderStatus }) {
  return useQuery({
    queryKey: ['work-orders', params],
    queryFn: async () => (await apiClient.get<WorkOrderListItem[]>('/api/work-orders', { params })).data,
  })
}

interface CreateWorkOrderInput {
  assetId: string
  type: WorkOrderType
  priority: WorkOrderPriority
  title: string
  description?: string
  scheduledDate?: string
}

export function useCreateWorkOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateWorkOrderInput) => (await apiClient.post<string>('/api/work-orders', input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-orders'] })
      queryClient.invalidateQueries({ queryKey: ['assets'] })
    },
  })
}

export function useUpdateWorkOrderStatus(workOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { status: WorkOrderStatus; cost?: number }) =>
      apiClient.put(`/api/work-orders/${workOrderId}/status`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-orders'] })
      queryClient.invalidateQueries({ queryKey: ['assets'] })
    },
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export type WorkOrderType = 'Preventive' | 'Corrective'
export type WorkOrderPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type WorkOrderStatus = 'Open' | 'InProgress' | 'PendingVerification' | 'Completed' | 'Cancelled'

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

export interface DowntimeLog {
  id: string
  startedAt: string
  endedAt: string | null
  reason: string | null
}

export interface WorkOrderDetail {
  id: string
  assetId: string
  assetName: string
  assetTag: string
  type: WorkOrderType
  priority: WorkOrderPriority
  status: WorkOrderStatus
  title: string
  description: string | null
  assignedToUserId: string | null
  scheduledDate: string | null
  completedDate: string | null
  cost: number | null
  downtimeLogs: DowntimeLog[]
  maintenanceScheduleId: string | null
  verificationNotes: string | null
  verifiedAt: string | null
}

export function useWorkOrdersList(params: {
  branchId?: string | null
  status?: WorkOrderStatus
  page?: number
  pageSize?: number
}) {
  return useQuery({
    queryKey: ['work-orders', params],
    queryFn: async () => (await apiClient.get<PagedList<WorkOrderListItem>>('/api/work-orders', { params })).data,
  })
}

export function useWorkOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['work-order', id],
    queryFn: async () => (await apiClient.get<WorkOrderDetail>(`/api/work-orders/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateWorkOrderInput {
  assetId: string
  type: WorkOrderType
  priority: WorkOrderPriority
  title: string
  description?: string
  scheduledDate?: string
  maintenanceScheduleId?: string
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
    mutationFn: async (input: { status: WorkOrderStatus }) => apiClient.put(`/api/work-orders/${workOrderId}/status`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-orders'] })
      queryClient.invalidateQueries({ queryKey: ['work-order', workOrderId] })
      queryClient.invalidateQueries({ queryKey: ['assets'] })
    },
  })
}

interface VerifyWorkOrderInput {
  approved: boolean
  cost?: number | null
  notes?: string | null
  nextDueDate?: string | null
}

export function useVerifyWorkOrder(workOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: VerifyWorkOrderInput) => apiClient.post(`/api/work-orders/${workOrderId}/verify`, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-orders'] })
      queryClient.invalidateQueries({ queryKey: ['work-order', workOrderId] })
      queryClient.invalidateQueries({ queryKey: ['assets'] })
      queryClient.invalidateQueries({ queryKey: ['maintenance-schedules'] })
    },
  })
}

export interface MaintenanceScheduleListItem {
  id: string
  assetId: string
  assetName: string
  recurrenceRule: string
  nextDueDate: string
  isActive: boolean
}

export function useMaintenanceSchedulesList(params: { branchId?: string | null }) {
  return useQuery({
    queryKey: ['maintenance-schedules', params],
    queryFn: async () => (await apiClient.get<MaintenanceScheduleListItem[]>('/api/work-orders/schedules', { params })).data,
  })
}

interface CreateMaintenanceScheduleInput {
  assetId: string
  recurrenceRule: string
  nextDueDate: string
}

export function useCreateMaintenanceSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateMaintenanceScheduleInput) =>
      (await apiClient.post<string>('/api/work-orders/schedules', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['maintenance-schedules'] }),
  })
}

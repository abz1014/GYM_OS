import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { PagedList } from '@/types/paging'

export interface AttendanceRecord {
  id: string
  memberId: string
  memberName: string
  checkInAt: string
  checkOutAt: string | null
  method: 'QrSimulated' | 'Manual'
}

export function useAttendanceHistory(params: { branchId?: string | null; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: ['attendance', params],
    queryFn: async () => (await apiClient.get<PagedList<AttendanceRecord>>('/api/attendance', { params })).data,
  })
}

export function useCheckIn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { memberId: string; branchId: string; method: 'QrSimulated' | 'Manual' }) =>
      (await apiClient.post<string>('/api/attendance/check-in', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance'] }),
  })
}

export function useCheckOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (attendanceRecordId: string) => apiClient.post(`/api/attendance/${attendanceRecordId}/check-out`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance'] }),
  })
}

export interface PeakHourBucket {
  hourOfDay: number
  checkInCount: number
}

export function usePeakHours(params: { branchId?: string | null; fromDate: string; toDate: string }) {
  return useQuery({
    queryKey: ['peak-hours', params],
    queryFn: async () => (await apiClient.get<PeakHourBucket[]>('/api/attendance/peak-hours', { params })).data,
  })
}

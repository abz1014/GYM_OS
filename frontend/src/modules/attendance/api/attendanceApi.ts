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

/**
 * `memberId` and `fromDate` were always supported by GET /api/attendance — they just had no caller
 * here. The front desk needs both: "is this person already inside?" is a memberId + checkedInOnly
 * lookup, and "how many times have they been in this month?" is a memberId + fromDate count read off
 * `totalCount`. Surfacing existing query parameters beats adding an endpoint for either.
 *
 * `enabled` exists because both of those are dependent queries — with no member selected the params
 * would degrade into "every attendance record at this branch", which is a real (and expensive) answer
 * to a question nobody asked.
 */
export function useAttendanceHistory(
  params: {
    branchId?: string | null
    memberId?: string
    searchTerm?: string
    checkedInOnly?: boolean
    fromDate?: string
    toDate?: string
    page?: number
    pageSize?: number
  },
  options?: { enabled?: boolean }
) {
  return useQuery({
    queryKey: ['attendance', params],
    queryFn: async () => (await apiClient.get<PagedList<AttendanceRecord>>('/api/attendance', { params })).data,
    enabled: options?.enabled ?? true,
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

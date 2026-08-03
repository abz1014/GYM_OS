import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface RevenueReportPoint {
  period: string
  revenue: number
}

export interface AttendanceReportPoint {
  date: string
  checkIns: number
}

export interface MembershipBreakdown {
  byStatus: Record<string, number>
  byPlanType: Record<string, number>
}

export function useRevenueReport(monthsBack = 6) {
  return useQuery({
    queryKey: ['reports', 'revenue', monthsBack],
    queryFn: async () =>
      (await apiClient.get<RevenueReportPoint[]>('/api/reports/revenue', { params: { monthsBack } })).data,
  })
}

export function useAttendanceReport(daysBack = 30) {
  return useQuery({
    queryKey: ['reports', 'attendance', daysBack],
    queryFn: async () =>
      (await apiClient.get<AttendanceReportPoint[]>('/api/reports/attendance', { params: { daysBack } })).data,
  })
}

export function useMembershipBreakdownReport() {
  return useQuery({
    queryKey: ['reports', 'membership-breakdown'],
    queryFn: async () => (await apiClient.get<MembershipBreakdown>('/api/reports/membership-breakdown')).data,
  })
}

async function downloadFile(url: string, params: Record<string, unknown>, filename: string) {
  const response = await apiClient.get<Blob>(url, { params, responseType: 'blob' })
  const objectUrl = window.URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = objectUrl
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(objectUrl)
}

export const exportRevenueReport = (monthsBack = 6) =>
  downloadFile('/api/reports/revenue/export', { monthsBack }, 'revenue-report.xlsx')

export const exportAttendanceReport = (daysBack = 30) =>
  downloadFile('/api/reports/attendance/export', { daysBack }, 'attendance-report.xlsx')

export const exportMembershipReport = () =>
  downloadFile('/api/reports/membership-breakdown/export', {}, 'membership-report.xlsx')

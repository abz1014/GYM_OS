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

export interface TrainerCommissionReportRow {
  trainerName: string
  totalPending: number
  totalPaid: number
  recordCount: number
}

export interface EquipmentDowntimeReportRow {
  assetName: string
  assetTag: string
  incidents: number
  totalDowntimeHours: number
  totalMaintenanceCost: number
}

export interface InventoryStockMovementReportRow {
  itemName: string
  sku: string
  totalIn: number
  totalOut: number
  netChange: number
  currentQuantityOnHand: number
}

export interface CrmPipelineConversionReport {
  byStage: Record<string, number>
  totalLeads: number
  convertedCount: number
  conversionRatePercent: number
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

export function useTrainerCommissionReport(monthsBack = 6) {
  return useQuery({
    queryKey: ['reports', 'trainer-commissions', monthsBack],
    queryFn: async () =>
      (await apiClient.get<TrainerCommissionReportRow[]>('/api/reports/trainer-commissions', { params: { monthsBack } })).data,
  })
}

export function useEquipmentDowntimeReport(monthsBack = 6) {
  return useQuery({
    queryKey: ['reports', 'equipment-downtime', monthsBack],
    queryFn: async () =>
      (await apiClient.get<EquipmentDowntimeReportRow[]>('/api/reports/equipment-downtime', { params: { monthsBack } })).data,
  })
}

export function useInventoryStockMovementReport(daysBack = 30) {
  return useQuery({
    queryKey: ['reports', 'inventory-stock-movement', daysBack],
    queryFn: async () =>
      (await apiClient.get<InventoryStockMovementReportRow[]>('/api/reports/inventory-stock-movement', { params: { daysBack } })).data,
  })
}

export function useCrmPipelineConversionReport() {
  return useQuery({
    queryKey: ['reports', 'crm-pipeline'],
    queryFn: async () => (await apiClient.get<CrmPipelineConversionReport>('/api/reports/crm-pipeline')).data,
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

export const exportTrainerCommissionReport = (monthsBack = 6) =>
  downloadFile('/api/reports/trainer-commissions/export', { monthsBack }, 'trainer-commission-report.xlsx')

export const exportEquipmentDowntimeReport = (monthsBack = 6) =>
  downloadFile('/api/reports/equipment-downtime/export', { monthsBack }, 'equipment-downtime-report.xlsx')

export const exportInventoryStockMovementReport = (daysBack = 30) =>
  downloadFile('/api/reports/inventory-stock-movement/export', { daysBack }, 'inventory-stock-movement-report.xlsx')

export const exportCrmPipelineReport = () =>
  downloadFile('/api/reports/crm-pipeline/export', {}, 'crm-pipeline-report.xlsx')

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

export interface WorkoutActivityReportRow {
  exerciseName: string
  muscleGroup: string | null
  timesLogged: number
  totalSets: number
  totalReps: number
  avgWeightKg: number | null
}

export interface NutritionFoodItemRow {
  foodItemName: string
  timesLogged: number
  totalCaloriesLogged: number
}

export interface NutritionReport {
  topFoodItems: NutritionFoodItemRow[]
  totalMealEntriesLogged: number
  totalCaloriesLogged: number
  totalWaterLogsLogged: number
  totalWaterMlLogged: number
}

export interface AtRiskMemberRow {
  memberId: string
  fullName: string
  memberCode: string
  lastCheckInDate: string
  daysSinceLastVisit: number
}

export interface CohortRetentionPoint {
  cohortMonth: string
  cohortSize: number
  stillActiveCount: number
  retentionRatePercent: number
}

export interface LtvBySourceRow {
  source: string
  memberCount: number
  totalRevenue: number
  averageLtv: number
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

export function useWorkoutActivityReport(daysBack = 30) {
  return useQuery({
    queryKey: ['reports', 'workout-activity', daysBack],
    queryFn: async () =>
      (await apiClient.get<WorkoutActivityReportRow[]>('/api/reports/workout-activity', { params: { daysBack } })).data,
  })
}

export function useNutritionReport(daysBack = 30) {
  return useQuery({
    queryKey: ['reports', 'nutrition', daysBack],
    queryFn: async () => (await apiClient.get<NutritionReport>('/api/reports/nutrition', { params: { daysBack } })).data,
  })
}

/** One week of capture figures. */
export interface CaptureRatePoint {
  weekStart: string
  visitDays: number
  loggedVisitDays: number
  orphanLogDays: number
  captureRatePercent: number
}

/**
 * How much of what happens in the gym the app actually captures. Everything the member experience
 * gives back — XP, records, streaks, leaderboards — is computed from recorded sessions, so a gym
 * that trains hard and logs nothing has an engine running on air, and no other report shows it.
 */
export interface LoggingCaptureReport {
  windowStart: string
  windowEnd: string
  totalVisitDays: number
  totalLoggedVisitDays: number
  totalOrphanLogDays: number
  captureRatePercent: number
  /** False when too many workouts were logged on days with no visit — the rate has stopped
   *  describing gym behaviour and shouldn't be read as progress. */
  isReliable: boolean
  membersWhoVisited: number
  membersWhoLogged: number
  membersVisitingWithoutLogging: number
  weekly: CaptureRatePoint[]
  /** Median minutes from arriving to the session being recorded. Null when nothing could be timed —
   *  never 0, which would read as "instant". */
  medianMinutesToLog: number | null
  latencyBuckets: LogLatencyBucket[]
  /** Null when nobody visited. Guards the capture rate: a ratio that climbs while this falls means
   *  confirmation got easier, not that anyone trained more. */
  sessionsPerMemberPerWeek: number | null
}

export interface LogLatencyBucket {
  bucket: 'WithinTheHour' | 'SameDay' | 'NextDay' | 'Later'
  sessions: number
}

export interface ReturnRatePoint {
  weekNumber: number
  /** Members who joined long enough ago for this week to have finished. Members still inside their
   *  week N are in neither this nor returnedMembers — they have not failed to return, their answer
   *  is not in yet. Always show this alongside the percentage. */
  eligibleMembers: number
  returnedMembers: number
  ratePercent: number
}

export interface ReturnRateReport {
  asOf: string
  weeks: ReturnRatePoint[]
}

export function useReturnRateReport() {
  return useQuery({
    queryKey: ['reports', 'return-rate'],
    queryFn: async () => (await apiClient.get<ReturnRateReport>('/api/reports/return-rate')).data,
  })
}

export function useLoggingCaptureReport(weeksBack = 12) {
  return useQuery({
    queryKey: ['reports', 'logging-capture', weeksBack],
    queryFn: async () =>
      (await apiClient.get<LoggingCaptureReport>('/api/reports/logging-capture', { params: { weeksBack } })).data,
  })
}

export function useAtRiskMembersReport() {
  return useQuery({
    queryKey: ['reports', 'at-risk-members'],
    queryFn: async () => (await apiClient.get<AtRiskMemberRow[]>('/api/reports/at-risk-members')).data,
  })
}

export function useCohortRetentionReport(monthsBack = 12) {
  return useQuery({
    queryKey: ['reports', 'cohort-retention', monthsBack],
    queryFn: async () =>
      (await apiClient.get<CohortRetentionPoint[]>('/api/reports/cohort-retention', { params: { monthsBack } })).data,
  })
}

export function useLtvBySourceReport() {
  return useQuery({
    queryKey: ['reports', 'ltv-by-source'],
    queryFn: async () => (await apiClient.get<LtvBySourceRow[]>('/api/reports/ltv-by-source')).data,
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

export const exportWorkoutActivityReport = (daysBack = 30) =>
  downloadFile('/api/reports/workout-activity/export', { daysBack }, 'workout-activity-report.xlsx')

export const exportNutritionReport = (daysBack = 30) =>
  downloadFile('/api/reports/nutrition/export', { daysBack }, 'nutrition-report.xlsx')

export const exportAtRiskMembersReport = () =>
  downloadFile('/api/reports/at-risk-members/export', {}, 'at-risk-members-report.xlsx')

export const exportCohortRetentionReport = (monthsBack = 12) =>
  downloadFile('/api/reports/cohort-retention/export', { monthsBack }, 'cohort-retention-report.xlsx')

export const exportLtvBySourceReport = () =>
  downloadFile('/api/reports/ltv-by-source/export', {}, 'ltv-by-source-report.xlsx')

export interface LevelDistributionRow {
  level: number
  memberCount: number
}

export interface RetentionCorrelation {
  atRiskMemberCount: number
  atRiskAverageLevel: number
  activeMemberCount: number
  activeAverageLevel: number
}

export interface EngagementSummary {
  totalActiveMembers: number
  xpEarnedLast30Days: number
  membersWithActiveStreak: number
  challengeParticipants: number
  challengeCompletions: number
  levelDistribution: LevelDistributionRow[]
  retention: RetentionCorrelation
}

export function useEngagementSummary() {
  return useQuery({
    queryKey: ['engagement', 'summary'],
    queryFn: async () => (await apiClient.get<EngagementSummary>('/api/engagement/summary')).data,
  })
}

import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import type { PagedList } from '@/types/paging'
import type { InvoiceListItem } from '@/modules/billing/api/billingApi'
import type { CrmPipelineSummary } from '@/modules/crm/api/crmApi'
import type { AtRiskMemberRow } from '@/modules/reports/api/reportsApi'

export interface DashboardSummary {
  todayRevenue: number
  todayCashCollected: number
  activeMembersCount: number
  newMembersThisMonthCount: number
  expiringMembershipsNext7DaysCount: number
  todayAttendanceCount: number
  trainerScheduleTodayCount: number
  equipmentAlertsCount: number
  maintenanceRemindersCount: number
  inventoryAlertsCount: number
}

export function useDashboardSummary() {
  const branchId = useUiStore((s) => s.selectedBranchId)

  return useQuery({
    queryKey: ['dashboard-summary', branchId],
    queryFn: async () =>
      (await apiClient.get<DashboardSummary>('/api/dashboard/summary', { params: { branchId } })).data,
    enabled: !!branchId,
    refetchInterval: 30_000,
  })
}

/*
 * Everything below feeds panels the summary endpoint doesn't cover. They are declared here rather
 * than imported from the owning module's hook for two reasons that both bite in practice:
 *
 *  - Permission. /api/dashboard/summary needs only dashboard.view, but a receptionist holding that
 *    permission may hold none of billing.view / reports.view / crm.view / attendance.view. The
 *    module hooks have no `enabled` parameter, so reusing them would fire requests that answer 403,
 *    retry, and leave the panel stuck in an error state instead of simply not rendering.
 *  - Parameters. "Who is still in the building" needs the attendance list bounded to today, and
 *    useAttendanceHistory doesn't expose fromDate/toDate.
 *
 * Response shapes are imported as types from the module that owns the endpoint, so there is still
 * exactly one definition of each payload.
 */

/** The API's DateOnly params are calendar dates; toISOString() would shift the day for any non-UTC user. */
function toDateOnlyString(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

interface BranchOption {
  id: string
  name: string
  currency: string
}

/**
 * The branch behind the numbers. Its ISO currency code formats every money figure — the only honest
 * source for the symbol, since the summary endpoint returns bare decimals — and the branch count
 * decides whether panels fed by tenant-wide endpoints have to say so: "4 invoices overdue" means
 * something different on a one-branch tenant than on a three-branch one.
 *
 * Deliberately shares BranchSwitcher's ['branches'] cache: same endpoint, same response, and the
 * switcher has already fetched it by the time this screen mounts.
 */
export function useBranchScope(): { branch: BranchOption | null; branchCount: number } {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data } = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<BranchOption[]>('/api/branches')).data,
  })

  return {
    branch: data?.find((b) => b.id === branchId) ?? null,
    branchCount: data?.length ?? 0,
  }
}

export interface HourlyCheckIns {
  hourOfDay: number
  checkInCount: number
}

/** Check-ins bucketed by hour over a date range — the only occupancy-shaped data the system holds. */
export function useHourlyCheckIns(fromDate: string, toDate: string) {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const hasPermission = useAuthStore((s) => s.hasPermission)

  return useQuery({
    queryKey: ['dashboard-hourly-check-ins', branchId, fromDate, toDate],
    queryFn: async () =>
      (await apiClient.get<HourlyCheckIns[]>('/api/attendance/peak-hours', { params: { branchId, fromDate, toDate } })).data,
    enabled: !!branchId && hasPermission('attendance.view'),
  })
}

/** Today's date, stable for the lifetime of the render so query keys don't churn. */
export function todayDateOnly(): string {
  return toDateOnlyString(new Date())
}

/** The start of the 4-week window used as the "typical day" baseline: 28 full days ending yesterday. */
export const OCCUPANCY_BASELINE_DAYS = 28

export function occupancyBaselineRange(): { fromDate: string; toDate: string } {
  const yesterday = new Date()
  yesterday.setDate(yesterday.getDate() - 1)
  const start = new Date()
  start.setDate(start.getDate() - OCCUPANCY_BASELINE_DAYS)
  return { fromDate: toDateOnlyString(start), toDate: toDateOnlyString(yesterday) }
}

/**
 * People who checked in today and have not checked out. Bounded to today on purpose: the bare
 * "checked in only" filter also returns visits from previous days that nobody ever closed, which
 * would report a building holding twice as many people as walked through the door.
 */
export function useStillCheckedInCount() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const today = todayDateOnly()

  return useQuery({
    queryKey: ['dashboard-still-checked-in', branchId, today],
    queryFn: async () =>
      (
        await apiClient.get<PagedList<unknown>>('/api/attendance', {
          params: { branchId, checkedInOnly: true, fromDate: today, toDate: today, pageSize: 1 },
        })
      ).data.totalCount,
    enabled: !!branchId && hasPermission('attendance.view'),
    refetchInterval: 60_000,
  })
}

/**
 * One page big enough to hold every overdue invoice a gym realistically carries. The count is exact
 * whatever comes back (totalCount is server-side), but the outstanding total and the oldest due date
 * can only be derived when this page holds every row — see the callers, which drop those two facts
 * rather than report a partial sum as a total. /api/invoices has no branch filter, so this figure is
 * tenant-wide and is labelled as such.
 */
export const OVERDUE_INVOICE_PAGE_SIZE = 100

export function useOverdueInvoices() {
  const hasPermission = useAuthStore((s) => s.hasPermission)

  return useQuery({
    queryKey: ['dashboard-overdue-invoices'],
    queryFn: async () =>
      (
        await apiClient.get<PagedList<InvoiceListItem>>('/api/invoices', {
          params: { status: 'Overdue', pageSize: OVERDUE_INVOICE_PAGE_SIZE },
        })
      ).data,
    enabled: hasPermission('billing.view'),
    refetchInterval: 60_000,
  })
}

export interface OverdueInvoiceSummary {
  /** Server-side total, so always exact however many rows came back. */
  count: number
  /** Null unless the fetched page holds every overdue invoice AND they share one currency. */
  outstanding: { amount: number; currency: string } | null
  /** Days past due of the oldest one; null under the same condition. */
  oldestDays: number | null
}

/**
 * Lives here, next to the query it reads, so the KPI tile and the queue row can't end up applying
 * different rules to the same invoices. The two nulls are the rule: a sum over one page of a longer
 * list is a page total, and printing it as the amount the gym is owed would understate the debt by
 * however much fell off the end.
 */
export function summarizeOverdueInvoices(paged: PagedList<InvoiceListItem> | undefined): OverdueInvoiceSummary | null {
  if (!paged) {
    return null
  }

  const { items, totalCount } = paged
  const isComplete = items.length >= totalCount
  const currencies = new Set(items.map((i) => i.currency))

  const oldestDueDate = items.reduce<string | null>(
    (oldest, invoice) => (oldest === null || invoice.dueDate < oldest ? invoice.dueDate : oldest),
    null
  )

  return {
    count: totalCount,
    outstanding:
      isComplete && currencies.size === 1
        ? { amount: items.reduce((sum, i) => sum + i.amountOutstanding, 0), currency: [...currencies][0] }
        : null,
    oldestDays:
      isComplete && oldestDueDate !== null
        ? Math.max(0, Math.floor((Date.now() - new Date(`${oldestDueDate}T00:00:00`).getTime()) / 86_400_000))
        : null,
  }
}

/**
 * Active members with no check-in for ChurnRiskPolicy.InactivityThresholdDays — the same definition
 * the automated win-back job uses, so the dashboard and the automation can't disagree about who is
 * at risk. Tenant-wide: the report takes no branch parameter.
 */
export function useAtRiskMembers() {
  const hasPermission = useAuthStore((s) => s.hasPermission)

  return useQuery({
    queryKey: ['dashboard-at-risk-members'],
    queryFn: async () => (await apiClient.get<AtRiskMemberRow[]>('/api/reports/at-risk-members')).data,
    enabled: hasPermission('reports.view'),
  })
}

export function useLeadPipeline() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const hasPermission = useAuthStore((s) => s.hasPermission)

  return useQuery({
    queryKey: ['dashboard-lead-pipeline', branchId],
    queryFn: async () =>
      (await apiClient.get<CrmPipelineSummary>('/api/leads/summary', { params: { branchId } })).data,
    enabled: !!branchId && hasPermission('crm.view'),
  })
}

import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import { useUiStore } from '@/stores/uiStore'

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

import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { MemberDetail } from '@/modules/members/api/membersApi'
import type { AttendanceRecord } from '@/modules/attendance/api/attendanceApi'
import type { WorkoutLog } from '@/modules/workouts/api/workoutsApi'
import type { DietPlanListItem, WaterLog } from '@/modules/nutrition/api/nutritionApi'
import type { PagedList } from '@/types/paging'

/**
 * The member self-service surface — every request here resolves "whose data" server-side from
 * the JWT (see /api/me on the backend). None of these hooks take or send a memberId; that is the
 * point. Reuses the exact response types the staff-facing modules already declare rather than
 * redefining them, since the DTOs are identical.
 */

export function useMyProfile() {
  return useQuery({
    queryKey: ['portal', 'profile'],
    queryFn: async () => (await apiClient.get<MemberDetail>('/api/me')).data,
    retry: false,
  })
}

export function useMyAttendance(params: { page?: number; pageSize?: number } = {}) {
  return useQuery({
    queryKey: ['portal', 'attendance', params],
    queryFn: async () => (await apiClient.get<PagedList<AttendanceRecord>>('/api/me/attendance', { params })).data,
    retry: false,
  })
}

export function useMyWorkoutLogs() {
  return useQuery({
    queryKey: ['portal', 'workouts'],
    queryFn: async () => (await apiClient.get<WorkoutLog[]>('/api/me/workouts')).data,
    retry: false,
  })
}

export function useMyDietPlans() {
  return useQuery({
    queryKey: ['portal', 'diet-plans'],
    queryFn: async () => (await apiClient.get<DietPlanListItem[]>('/api/me/nutrition/diet-plans')).data,
    retry: false,
  })
}

export function useMyWaterLogs() {
  return useQuery({
    queryKey: ['portal', 'water'],
    queryFn: async () => (await apiClient.get<WaterLog[]>('/api/me/nutrition/water')).data,
    retry: false,
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { MemberDetail } from '@/modules/members/api/membersApi'
import type { AttendanceRecord } from '@/modules/attendance/api/attendanceApi'
import type { ClassBookingStatus } from '@/modules/classes/api/classesApi'
import type { WorkoutAssignmentListItem, WorkoutLog } from '@/modules/workouts/api/workoutsApi'
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

export function useMyWorkoutAssignments() {
  return useQuery({
    queryKey: ['portal', 'workout-assignments'],
    queryFn: async () => (await apiClient.get<WorkoutAssignmentListItem[]>('/api/me/workout-assignments')).data,
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

export interface MyClassSession {
  sessionId: string
  classTypeName: string
  colorHex: string | null
  trainerName: string | null
  startsAt: string
  durationMinutes: number
  capacity: number
  location: string | null
  bookedCount: number
  isFull: boolean
  myBookingStatus: ClassBookingStatus | null
  myBookingId: string | null
}

export interface MyClassBooking {
  bookingId: string
  sessionId: string
  classTypeName: string
  colorHex: string | null
  trainerName: string | null
  startsAt: string
  durationMinutes: number
  location: string | null
  status: ClassBookingStatus
}

export function useMyClassSchedule() {
  return useQuery({
    queryKey: ['portal', 'classes'],
    queryFn: async () => (await apiClient.get<MyClassSession[]>('/api/me/classes')).data,
    retry: false,
  })
}

export function useMyClassBookings() {
  return useQuery({
    queryKey: ['portal', 'class-bookings'],
    queryFn: async () => (await apiClient.get<MyClassBooking[]>('/api/me/class-bookings')).data,
    retry: false,
  })
}

function invalidateMyClasses(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['portal', 'classes'] })
  queryClient.invalidateQueries({ queryKey: ['portal', 'class-bookings'] })
}

export function useBookMyClass() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (sessionId: string) =>
      (await apiClient.post<ClassBookingStatus>(`/api/me/classes/${sessionId}/book`)).data,
    onSuccess: () => invalidateMyClasses(queryClient),
  })
}

export function useCancelMyClassBooking() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (bookingId: string) => apiClient.post(`/api/me/class-bookings/${bookingId}/cancel`),
    onSuccess: () => invalidateMyClasses(queryClient),
  })
}

export interface MyWeightPoint {
  measuredOn: string
  weightKg: number
}

export interface MyGoal {
  id: string
  title: string
  targetDate: string | null
  isAchieved: boolean
  achievedAt: string | null
}

export interface MyProgressPhoto {
  id: string
  photoUrl: string
  takenAt: string
  notes: string | null
}

export interface MyProgress {
  weeklyStreak: number
  totalVisits: number
  visitsThisMonth: number
  weightTrend: MyWeightPoint[]
  goals: MyGoal[]
  photos: MyProgressPhoto[]
}

export function useMyProgress() {
  return useQuery({
    queryKey: ['portal', 'progress'],
    queryFn: async () => (await apiClient.get<MyProgress>('/api/me/progress')).data,
    retry: false,
  })
}

export function useCreateMyGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (payload: { title: string; targetDate: string | null }) =>
      (await apiClient.post<string>('/api/me/goals', payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portal', 'progress'] }),
  })
}

export function useAchieveMyGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (goalId: string) => apiClient.post(`/api/me/goals/${goalId}/achieve`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portal', 'progress'] }),
  })
}

export interface MyReferrals {
  memberCode: string
  referralCount: number
  referredMembers: { firstName: string; joinDate: string }[]
}

export function useMyReferrals() {
  return useQuery({
    queryKey: ['portal', 'referrals'],
    queryFn: async () => (await apiClient.get<MyReferrals>('/api/me/referrals')).data,
    retry: false,
  })
}

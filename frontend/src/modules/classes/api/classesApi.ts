import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export type ClassSessionStatus = 'Scheduled' | 'Cancelled' | 'Completed'

export interface ClassType {
  id: string
  name: string
  description: string | null
  defaultDurationMinutes: number
  defaultCapacity: number
  colorHex: string | null
  isActive: boolean
}

export interface ClassSchedule {
  id: string
  classTypeId: string
  classTypeName: string
  colorHex: string | null
  trainerId: string | null
  trainerName: string | null
  dayOfWeek: string
  startTime: string
  durationMinutes: number
  capacity: number
  location: string | null
  isActive: boolean
}

export interface ClassSession {
  id: string
  classScheduleId: string | null
  classTypeId: string
  classTypeName: string
  colorHex: string | null
  trainerId: string | null
  trainerName: string | null
  startsAt: string
  durationMinutes: number
  capacity: number
  location: string | null
  status: ClassSessionStatus
  bookedCount: number
  waitlistCount: number
}

export type ClassBookingStatus = 'Booked' | 'Waitlisted' | 'CheckedIn' | 'NoShow' | 'Cancelled'

export interface ClassBooking {
  id: string
  memberId: string
  memberName: string
  memberCode: string
  status: ClassBookingStatus
  bookedAt: string
  checkedInAt: string | null
}

export interface ClassSessionRoster {
  sessionId: string
  classTypeId: string
  classTypeName: string
  startsAt: string
  capacity: number
  bookedCount: number
  waitlistCount: number
  status: ClassSessionStatus
  bookings: ClassBooking[]
}

export function useClassTypes() {
  return useQuery({
    queryKey: ['class-types'],
    queryFn: async () => (await apiClient.get<ClassType[]>('/api/classes/types')).data,
  })
}

export function useCreateClassType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      name: string
      description?: string
      defaultDurationMinutes: number
      defaultCapacity: number
      colorHex?: string
    }) => (await apiClient.post<string>('/api/classes/types', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['class-types'] }),
  })
}

export function useClassSchedules(params: { branchId?: string | null }) {
  return useQuery({
    queryKey: ['class-schedules', params],
    queryFn: async () => (await apiClient.get<ClassSchedule[]>('/api/classes/schedules', { params })).data,
  })
}

export function useCreateClassSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      branchId: string
      classTypeId: string
      trainerId?: string
      dayOfWeek: string
      startTime: string
      durationMinutes?: number
      capacity?: number
      location?: string
    }) => (await apiClient.post<string>('/api/classes/schedules', input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['class-schedules'] })
      queryClient.invalidateQueries({ queryKey: ['class-sessions'] })
    },
  })
}

export function useSetClassScheduleActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { id: string; isActive: boolean }) =>
      apiClient.put(`/api/classes/schedules/${input.id}/active`, { isActive: input.isActive }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['class-schedules'] })
      queryClient.invalidateQueries({ queryKey: ['class-sessions'] })
    },
  })
}

export function useClassSessions(params: { branchId?: string | null }) {
  return useQuery({
    queryKey: ['class-sessions', params],
    queryFn: async () => (await apiClient.get<ClassSession[]>('/api/classes/sessions', { params })).data,
  })
}

export function useCancelClassSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (sessionId: string) => apiClient.post(`/api/classes/sessions/${sessionId}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['class-sessions'] }),
  })
}

export function useClassSessionRoster(sessionId: string | undefined) {
  return useQuery({
    queryKey: ['class-roster', sessionId],
    queryFn: async () => (await apiClient.get<ClassSessionRoster>(`/api/classes/sessions/${sessionId}/roster`)).data,
    enabled: !!sessionId,
  })
}

// Shared invalidation for anything that changes a session's roster: the roster itself and the
// session list's booked/waitlist counts.
function invalidateRoster(queryClient: ReturnType<typeof useQueryClient>, sessionId: string) {
  queryClient.invalidateQueries({ queryKey: ['class-roster', sessionId] })
  queryClient.invalidateQueries({ queryKey: ['class-sessions'] })
}

export function useBookClassSession(sessionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (memberId: string) =>
      (await apiClient.post<ClassBookingStatus>(`/api/classes/sessions/${sessionId}/bookings`, { memberId })).data,
    onSuccess: () => invalidateRoster(queryClient, sessionId),
  })
}

export function useCancelClassBooking(sessionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (bookingId: string) => apiClient.post(`/api/classes/bookings/${bookingId}/cancel`),
    onSuccess: () => invalidateRoster(queryClient, sessionId),
  })
}

export function useRecordClassBookingAttendance(sessionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { bookingId: string; attended: boolean }) =>
      apiClient.post(`/api/classes/bookings/${input.bookingId}/attendance`, { attended: input.attended }),
    onSuccess: () => invalidateRoster(queryClient, sessionId),
  })
}

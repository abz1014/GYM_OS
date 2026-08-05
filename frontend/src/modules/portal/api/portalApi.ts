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

export interface MyNutritionSummary {
  activeDietPlanName: string | null
  targetCalories: number | null
  targetProteinG: number | null
  targetCarbsG: number | null
  targetFatG: number | null
  consumedCalories: number
  consumedProteinG: number
  consumedCarbsG: number
  consumedFatG: number
  waterMl: number
}

export function useMyNutritionSummary() {
  return useQuery({
    queryKey: ['portal', 'nutrition-summary'],
    queryFn: async () => (await apiClient.get<MyNutritionSummary>('/api/me/nutrition/summary')).data,
    retry: false,
  })
}

export type OverloadSuggestion = 'InsufficientData' | 'Progressing' | 'ReadyToIncreaseWeight' | 'ConsiderDeload'

export interface MyExerciseSuggestion {
  exerciseId: string
  exerciseName: string
  muscleGroup: string | null
  suggestion: OverloadSuggestion
  lastWeightKg: number | null
  lastTotalReps: number | null
  suggestedNextWeightKg: number | null
  lastLoggedAt: string
}

export function useMyWorkoutSuggestions() {
  return useQuery({
    queryKey: ['portal', 'workout-suggestions'],
    queryFn: async () => (await apiClient.get<MyExerciseSuggestion[]>('/api/me/workout-suggestions')).data,
    retry: false,
  })
}

export interface MyXpEntry {
  amount: number
  reason: string
  occurredAt: string
}

export interface MyExperience {
  level: number
  totalXp: number
  xpIntoLevel: number
  xpForNextLevel: number
  recent: MyXpEntry[]
}

export function useMyExperience() {
  return useQuery({
    queryKey: ['portal', 'experience'],
    queryFn: async () => (await apiClient.get<MyExperience>('/api/me/experience')).data,
    retry: false,
  })
}

export interface MyPersonalRecord {
  exerciseId: string
  exerciseName: string
  type: 'MaxWeight' | 'EstimatedOneRepMax' | 'SessionVolume'
  value: number
  achievedAt: string
}

export function useMyPersonalRecords() {
  return useQuery({
    queryKey: ['portal', 'personal-records'],
    queryFn: async () => (await apiClient.get<MyPersonalRecord[]>('/api/me/personal-records')).data,
    retry: false,
  })
}

export interface ExerciseMastery {
  exerciseId: string
  exerciseName: string
  muscleGroup: string | null
  equipment: string | null
  sessions: number
  bestWeightKg: number
  bestEstimatedOneRepMax: number
  totalVolume: number
  masteryPercent: number
  lastTrainedAt: string
}

export interface GroupMastery {
  name: string
  sessions: number
  totalVolume: number
  masteryPercent: number
}

export interface MyMastery {
  exercises: ExerciseMastery[]
  muscleGroups: GroupMastery[]
  machines: GroupMastery[]
}

export function useMyMastery() {
  return useQuery({
    queryKey: ['portal', 'mastery'],
    queryFn: async () => (await apiClient.get<MyMastery>('/api/me/mastery')).data,
    retry: false,
  })
}

export type AchievementTier = 'Bronze' | 'Silver' | 'Gold' | 'Platinum'

export interface MyAchievement {
  code: string
  name: string
  description: string
  tier: AchievementTier
  category: string
  icon: string
  unlocked: boolean
  unlockedAt: string | null
}

export function useMyAchievements() {
  return useQuery({
    queryKey: ['portal', 'achievements'],
    queryFn: async () => (await apiClient.get<MyAchievement[]>('/api/me/achievements')).data,
    retry: false,
  })
}

export interface MyStreaks {
  attendanceWeeks: number
  workoutWeeks: number
  nutritionWeeks: number
}

export function useMyStreaks() {
  return useQuery({
    queryKey: ['portal', 'streaks'],
    queryFn: async () => (await apiClient.get<MyStreaks>('/api/me/streaks')).data,
    retry: false,
  })
}

export type RecoveryStatus = 'Fresh' | 'Ready' | 'Fatigued' | 'OvertrainingRisk'
export type RecoveryKind = 'RestDay' | 'ActiveRecovery' | 'Mobility' | 'Stretching'

export interface MuscleRecovery {
  muscleGroup: string
  status: RecoveryStatus
  reason: string
  timesLast7Days: number
  daysSinceLastTrained: number | null
}

export interface MyRecovery {
  status: RecoveryStatus
  reason: string
  sessionsLast7Days: number
  restDaysLast7Days: number
  daysSinceLastWorkout: number | null
  muscleGroups: MuscleRecovery[]
}

export function useMyRecovery() {
  return useQuery({
    queryKey: ['portal', 'recovery'],
    queryFn: async () => (await apiClient.get<MyRecovery>('/api/me/recovery')).data,
    retry: false,
  })
}

export function useLogMyRecovery() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (payload: { kind: RecoveryKind; notes: string | null }) =>
      (await apiClient.post<string>('/api/me/recovery/log', payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['portal', 'recovery'] })
      queryClient.invalidateQueries({ queryKey: ['portal', 'experience'] })
    },
  })
}

export type RecommendationType =
  | 'PlateauAlert'
  | 'WeeklyFocus'
  | 'VolumeSuggestion'
  | 'ExerciseSubstitution'
  | 'RecoveryAdvice'
  | 'TrainerPlanActive'

export interface MyRecommendation {
  type: RecommendationType
  title: string
  explanation: string
  exerciseId: string | null
}

export function useMyRecommendations() {
  return useQuery({
    queryKey: ['portal', 'recommendations'],
    queryFn: async () => (await apiClient.get<MyRecommendation[]>('/api/me/recommendations')).data,
    retry: false,
  })
}

export type TimelineEntryType = 'Measurement' | 'Photo' | 'GoalAchieved' | 'PersonalRecord' | 'Achievement'

export interface MyTimelineEntry {
  type: TimelineEntryType
  occurredAt: string
  title: string
  description: string | null
  photoUrl: string | null
}

export function useMyTimeline() {
  return useQuery({
    queryKey: ['portal', 'timeline'],
    queryFn: async () => (await apiClient.get<MyTimelineEntry[]>('/api/me/timeline')).data,
    retry: false,
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { apiClient } from '@/lib/apiClient'
import type { CoachMessageSession } from '@/shared/components/coaching/SessionAttachment'
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

/*
 * Achievements and streaks have no hook of their own. Each had one, neither had a screen: the app
 * already tells the member both, in the place each belongs — achievements inside the session they
 * happened on the timeline, the streak on the home screen. A hook nothing calls is a feature that
 * looks shipped from the code and is invisible from the app.
 *
 * Their endpoints stay. They are self-scoped, tested, and the obvious thing a second client would ask
 * for; it is the unused wiring that was the dead weight, not the API — which is exactly why records
 * got their hook back below the moment a screen actually needed them.
 */
export interface MyPersonalRecord {
  exerciseId: string
  exerciseName: string
  type: 'MaxWeight' | 'EstimatedOneRepMax' | 'SessionVolume'
  value: number
  achievedAt: string
}

/** Read by the live session, to tell a member when the set in front of them is worth a record. */
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

export interface MyStreaks {
  attendanceWeeks: number
  workoutWeeks: number
  nutritionWeeks: number
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

/**
 * The whole home screen in one response. Previously assembled here in the browser from five separate
 * queries, which meant the ring, the streak and the nudge could each be from a different moment —
 * and the session count was derived client-side from training volume, so a session logged without
 * weights didn't count. The server now owns that rule (WeeklyGoalPolicy) and hands back the answer.
 */
export interface MyToday {
  firstName: string
  sessionsThisWeek: number
  weeklySessionGoal: number
  remainingSessions: number
  goalMet: boolean
  workoutStreakWeeks: number
  /** Seven flags, Monday first, for the day strip under the ring. Same rule, same rows as the count. */
  daysTrainedThisWeek: boolean[]
  nextClassToday: MyClassBooking | null
  /** At most two, ranked by what the member can act on today. See TrainingInsightPolicy. */
  insights: MyInsight[]
  visit: MyVisit
  /** The nearest thing the member has ahead of them, or null when there honestly isn't one. */
  coming: MyAnticipation | null
  /** Messages from their coach they haven't opened. Drives the tab-bar dot — see MemberTabBar. */
  unreadCoachMessages: number
}

export interface MyAnticipation {
  kind: 'BookedClass' | 'Challenge' | 'Level'
  title: string
  detail: string
}

/** Today's gym visit as the turnstile already knows it. See VisitPolicy. */
export interface MyInsight {
  kind: 'RecoveryAlert' | 'ReadyForPr' | 'Comeback' | 'Plateau' | 'GoneQuiet' | 'Momentum'
  title: string
  detail: string
}

export interface MyVisit {
  state: 'None' | 'InGym' | 'Visited'
  checkedInAt: string | null
  sessionRecorded: boolean
  /** They were here today and nothing was written down. */
  needsRecording: boolean
}

export function useMyToday() {
  return useQuery({
    queryKey: ['portal', 'today'],
    queryFn: async () => (await apiClient.get<MyToday>('/api/me/today')).data,
    retry: false,
  })
}

export type LeaderboardCategory = 'XpEarned' | 'WorkoutsLogged' | 'GymVisits' | 'WeeklyStreak'
export type LeaderboardPeriod = 'Month' | 'Week'

export interface LeaderboardRow {
  rank: number
  displayName: string
  score: number
  isYou: boolean
}

/**
 * A branch leaderboard as one member sees it: the podium, where they sit, and who is either side.
 * Never the whole gym — the full board is unbounded and mostly irrelevant to any one member.
 */
export interface MyLeaderboard {
  category: LeaderboardCategory
  period: LeaderboardPeriod
  windowStart: string
  windowEnd: string
  totalRanked: number
  podium: LeaderboardRow[]
  aroundYou: LeaderboardRow[]
  you: LeaderboardRow | null
  yourPercentile: number | null
}

export function useMyLeaderboard(category: LeaderboardCategory, period: LeaderboardPeriod) {
  return useQuery({
    queryKey: ['portal', 'leaderboard', category, period],
    queryFn: async () =>
      (await apiClient.get<MyLeaderboard>('/api/me/leaderboard', { params: { category, period } })).data,
    retry: false,
  })
}

export function useSetMyWeeklyGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (weeklySessionGoal: number) => {
      await apiClient.put('/api/me/weekly-goal', { weeklySessionGoal })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portal'] }),
  })
}

export type TimelineEntryType =
  | 'Workout'
  | 'Measurement'
  | 'Photo'
  | 'GoalAchieved'
  /** Only for a record with no session attached — one set during a session is folded into it. */
  | 'PersonalRecord'
  | 'Achievement'

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

// ---------------------------------------------------------------------------
// Member self-logging. Until these existed a member could see every projection
// the experience engine produced but had no way to produce one — only staff
// could log on their behalf.
// ---------------------------------------------------------------------------

export interface LoggableExercise {
  id: string
  name: string
  muscleGroup: string | null
  equipment: string | null
}

export interface LoggableFood {
  id: string
  name: string
  caloriesPerServing: number
  proteinG: number
  servingSizeDescription: string
}

export interface MyLoggingOptions {
  exercises: LoggableExercise[]
  foods: LoggableFood[]
  activeDietPlanName: string | null
}

export function useMyLoggingOptions() {
  return useQuery({
    queryKey: ['portal', 'logging-options'],
    queryFn: async () => (await apiClient.get<MyLoggingOptions>('/api/me/logging-options')).data,
    retry: false,
  })
}

export interface MyMeasurement {
  id: string
  measuredOn: string
  weightKg: number | null
  bodyFatPercentage: number | null
  chestCm: number | null
  waistCm: number | null
  hipCm: number | null
  armCm: number | null
  thighCm: number | null
  notes: string | null
}

export function useMyMeasurements() {
  return useQuery({
    queryKey: ['portal', 'measurements'],
    queryFn: async () => (await apiClient.get<MyMeasurement[]>('/api/me/measurements')).data,
    retry: false,
  })
}

export interface MyDailyVolume {
  date: string
  volumeKg: number
  totalReps: number
}

export function useMyTrainingVolume(daysBack = 30) {
  return useQuery({
    queryKey: ['portal', 'training-volume', daysBack],
    queryFn: async () =>
      (await apiClient.get<MyDailyVolume[]>('/api/me/training-volume', { params: { daysBack } })).data,
    retry: false,
  })
}

export interface WorkoutEntryInput {
  exerciseId: string
  setsCompleted: number
  repsCompleted: number
  weightKg: number | null
}

/**
 * Logging any activity moves almost every member-facing read (XP, records, mastery, streaks,
 * recovery, recommendations, timeline, challenges), so each mutation invalidates the whole portal
 * namespace rather than trying to enumerate which cards happened to change.
 */
function invalidateAfterLogging(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['portal'] })
}

/**
 * What the engine actually did with a logged session. Every figure is a real diff computed
 * server-side — the screen that shows this used to assert a flat "+50 XP" regardless of what was
 * awarded, which was simply untrue once personal-record and achievement bonuses existed.
 */
export interface MyNewRecord {
  exerciseName: string
  type: string
  value: number
}

export interface MyNewAchievement {
  code: string
  name: string
  description: string
  tier: string
  icon: string
}

export interface MyChallengeStep {
  name: string
  workoutsLogged: number
  targetWorkoutCount: number
  justCompleted: boolean
}

export interface MyWorkoutResult {
  workoutLogId: string
  /** What the session was — "Push day", "Legs", "Back & Arms" — derived from what was trained. */
  character: string
  xpEarned: number
  level: number
  leveledUp: boolean
  sessionsThisWeek: number
  weeklySessionGoal: number
  goalMet: boolean
  /** True only for the session that closed the ring, not every session after it. */
  goalJustMet: boolean
  workoutStreakWeeks: number
  newRecords: MyNewRecord[]
  newAchievements: MyNewAchievement[]
  challengeProgress: MyChallengeStep[]
}

export type SessionProposalSource = 'None' | 'TrainerPlan' | 'RepeatLast' | 'Starter'

/**
 * What the proposal is based on, said plainly. Kept beside the type so every surface that offers the
 * session names it the same way — a member who is told "today's plan" on one screen and "same as
 * last time" on another has been shown two different sessions by the same app.
 */
export const SESSION_SOURCE_LABEL: Record<SessionProposalSource, string> = {
  TrainerPlan: "Today's plan",
  RepeatLast: 'Same as last time',
  Starter: 'Start with the basics',
  None: '',
}

export interface ProposedEntry {
  exerciseId: string
  exerciseName: string
  sets: number
  reps: number
  weightKg: number | null
}

/**
 * The session the app believes the member is about to do, pre-filled so recording it is one tap.
 * `source` says where it came from — a member should never be shown numbers with no provenance.
 */
export interface MySessionProposal {
  source: SessionProposalSource
  canConfirm: boolean
  entries: ProposedEntry[]
}

export function useMyNextSession() {
  return useQuery({
    queryKey: ['portal', 'next-session'],
    queryFn: async () => (await apiClient.get<MySessionProposal>('/api/me/next-session')).data,
    retry: false,
  })
}

/** How long the "Undo" on the confirmation toast stays reachable. */
export const UNDO_TOAST_MS = 20_000

/**
 * The one implementation of taking a session back. Shared by the button on the celebration and by
 * the toast that outlives it, so there is a single call and a single cache invalidation however the
 * member reaches for it. Takes the query client rather than being a hook, because the toast fires
 * after the component that raised it has gone.
 */
async function undoWorkout(queryClient: ReturnType<typeof useQueryClient>, workoutLogId: string) {
  await apiClient.delete(`/api/me/workouts/${workoutLogId}`)
  invalidateAfterLogging(queryClient)
}

/** Takes back a just-logged session. The server enforces the window and unwinds XP and records. */
export function useUndoMyWorkout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workoutLogId: string) => undoWorkout(queryClient, workoutLogId),
  })
}

/**
 * Offers the way back for a few seconds after the member closes the celebration.
 *
 * One tap to log makes a mis-tap equally cheap, and the person most likely to mis-tap is also the
 * one most likely to dismiss reflexively — so the recovery has to survive the screen it was on.
 * Until now it did not: closing the celebration took the only "I didn't do this" with it, and the
 * member was left with XP, possibly a record, and no way to say it never happened.
 *
 * Returned as a plain function bound to the query client, not a mutation, so it keeps working after
 * the celebration unmounts.
 */
export function useOfferUndo() {
  const queryClient = useQueryClient()
  return (workoutLogId: string, character: string) =>
    toast(`${character} saved.`, {
      duration: UNDO_TOAST_MS,
      action: {
        label: 'Undo',
        onClick: async () => {
          try {
            await undoWorkout(queryClient, workoutLogId)
            toast.success('Workout removed.')
          } catch {
            toast.error('That workout can no longer be undone.')
          }
        },
      },
    })
}

export function useLogMyWorkout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (entries: WorkoutEntryInput[]) =>
      (await apiClient.post<MyWorkoutResult>('/api/me/workouts', { entries })).data,
    onSuccess: () => invalidateAfterLogging(queryClient),
  })
}

export function useLogMyWater() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (amountMl: number) => (await apiClient.post<string>('/api/me/nutrition/water', { amountMl })).data,
    onSuccess: () => invalidateAfterLogging(queryClient),
  })
}

export interface LogMealInput {
  foodItemId: string
  mealType: 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'
  quantity: number
}

export function useLogMyMeal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LogMealInput) => (await apiClient.post<string>('/api/me/nutrition/meals', input)).data,
    onSuccess: () => invalidateAfterLogging(queryClient),
  })
}

export interface LogMeasurementInput {
  weightKg: number | null
  bodyFatPercentage: number | null
  chestCm: number | null
  waistCm: number | null
  hipCm: number | null
  armCm: number | null
  thighCm: number | null
  notes: string | null
}

export function useLogMyMeasurement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LogMeasurementInput) =>
      (await apiClient.post<string>('/api/me/measurements', input)).data,
    onSuccess: () => invalidateAfterLogging(queryClient),
  })
}

// ---------------------------------------------------------------------------
// The member and their trainer, talking. Which trainer is resolved server-side from the member's own
// assignment — there is no id here for reaching anyone else's coach.

export interface MyCoachMessage {
  id: string
  author: 'Member' | 'Trainer'
  body: string
  sentAt: string
  read: boolean
  /** The session this message is about, when it is about one. */
  workoutLogId: string | null
  /** Resolved server-side; null when about nothing, or the workout was since deleted. */
  session: CoachMessageSession | null
}

export interface MyCoach {
  trainerId: string | null
  trainerName: string | null
  /** False once a pairing ends: the history stays readable, but there is nobody to add to it. */
  canSend: boolean
  unreadCount: number
  /** More exists above the window — the conversation is capped so a long one is not downloaded whole. */
  hasOlder: boolean
  messages: MyCoachMessage[]
}

export function useMyCoach() {
  return useQuery({
    queryKey: ['portal', 'coach'],
    queryFn: async () => (await apiClient.get<MyCoach>('/api/me/coach')).data,
    retry: false,
  })
}

export function useMessageMyCoach() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (payload: { body: string; workoutLogId?: string | null }) =>
      (await apiClient.post<string>('/api/me/coach/messages', payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portal', 'coach'] }),
  })
}

export function useReadMyCoachMessages() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => apiClient.post('/api/me/coach/messages/read'),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['portal', 'coach'] }),
  })
}

// ---------------------------------------------------------------------------
// The member's map of their own gym. Every other surface reports what they trained; this is the only
// one that knows what they have not touched, because it leads with the gym's catalogue.

export interface MyPassportEntry {
  exerciseId: string
  exerciseName: string
  muscleGroup: string | null
  tried: boolean
  sessions: number
  /** Null for a movement carrying no load — "0kg" on a plank would read as a failure. */
  bestWeightKg: number | null
  lastTrained: string | null
  daysSinceLastTrained: number | null
  /** Tried once and then left alone long enough to have dropped out of their training. */
  goneQuiet: boolean
}

export interface MyPassportStamp {
  equipment: string
  tried: number
  available: number
  complete: boolean
  entries: MyPassportEntry[]
}

export interface MyPassport {
  tried: number
  /** Size of this gym's own catalogue — coverage is measured against the site they actually train at. */
  available: number
  percentCovered: number
  complete: boolean
  stamps: MyPassportStamp[]
  /** The few they have drifted furthest from — bounded, so it stays a prompt not a backlog. */
  goneQuiet: MyPassportEntry[]
}

export function useMyPassport() {
  return useQuery({
    queryKey: ['portal', 'passport'],
    queryFn: async () => (await apiClient.get<MyPassport>('/api/me/passport')).data,
    retry: false,
  })
}

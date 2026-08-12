import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

/**
 * How a movement is measured. Not a display detail — the write path REJECTS a rep count on a run
 * and a load on a plank, so the logging form has to ask for the right fields or it produces a 400.
 */
export type ExerciseLoadType = 'Weighted' | 'Bodyweight' | 'Timed' | 'Distance'

export interface Exercise {
  id: string
  name: string
  muscleGroup: string | null
  equipment: string | null
  description: string | null
  videoUrl: string | null
  loadType: ExerciseLoadType
}

export interface WorkoutTemplateListItem {
  id: string
  name: string
  description: string | null
  exerciseCount: number
}

export interface WorkoutTemplateExerciseItem {
  id: string
  exerciseId: string
  exerciseName: string
  setsCount: number
  repsCount: number
  orderIndex: number
}

export interface WorkoutTemplateDetail {
  id: string
  name: string
  description: string | null
  exercises: WorkoutTemplateExerciseItem[]
}

export function useExercisesList() {
  return useQuery({
    queryKey: ['exercises'],
    queryFn: async () => (await apiClient.get<Exercise[]>('/api/workouts/exercises')).data,
  })
}

export function useCreateExercise() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { name: string; muscleGroup?: string; equipment?: string; description?: string }) =>
      (await apiClient.post<string>('/api/workouts/exercises', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exercises'] }),
  })
}

export function useWorkoutTemplatesList() {
  return useQuery({
    queryKey: ['workout-templates'],
    queryFn: async () => (await apiClient.get<WorkoutTemplateListItem[]>('/api/workouts/templates')).data,
  })
}

export function useWorkoutTemplate(id: string | undefined) {
  return useQuery({
    queryKey: ['workout-template', id],
    queryFn: async () => (await apiClient.get<WorkoutTemplateDetail>(`/api/workouts/templates/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateTemplateInput {
  name: string
  description?: string
  exercises: { exerciseId: string; setsCount: number; repsCount: number; orderIndex: number }[]
}

export function useCreateWorkoutTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateTemplateInput) => (await apiClient.post<string>('/api/workouts/templates', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['workout-templates'] }),
  })
}

/**
 * One logged set. Every measurement except the set count is nullable, because which ones exist is
 * decided by the movement: a run has a distance and a duration, a plank has a duration alone, and a
 * press-up has reps and no load. Null means "this movement has no such measurement" — never zero.
 */
export interface WorkoutLogEntry {
  id: string
  exerciseId: string
  exerciseName: string
  setsCompleted: number
  repsCompleted: number | null
  weightKg: number | null
  durationSeconds: number | null
  distanceMeters: number | null
}

export interface WorkoutLog {
  id: string
  memberId: string
  workoutTemplateId: string | null
  workoutTemplateName: string | null
  /**
   * What the session was, derived from the muscle groups trained — "Push day", "Legs",
   * "Back & Arms". Always present. Prefer `workoutTemplateName` when it exists: a trainer's own
   * name for their session beats anything derived from it.
   */
  character: string
  loggedAt: string
  entries: WorkoutLogEntry[]
}

export function useMemberWorkoutLogs(memberId: string | undefined) {
  return useQuery({
    queryKey: ['workout-logs', memberId],
    queryFn: async () => (await apiClient.get<WorkoutLog[]>(`/api/workouts/logs/member/${memberId}`)).data,
    enabled: !!memberId,
  })
}

interface LogWorkoutInput {
  memberId: string
  workoutTemplateId?: string
  entries: {
    exerciseId: string
    setsCompleted: number
    repsCompleted?: number | null
    weightKg?: number | null
    durationSeconds?: number | null
    distanceMeters?: number | null
  }[]
}

export function useLogWorkout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LogWorkoutInput) => (await apiClient.post<string>('/api/workouts/logs', input)).data,
    onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: ['workout-logs', variables.memberId] }),
  })
}

export interface WorkoutAssignmentListItem {
  id: string
  workoutTemplateId: string
  workoutTemplateName: string
  templateDescription: string | null
  startDate: string
  endDate: string | null
  notes: string | null
  exercises: { id: string; exerciseId: string; exerciseName: string; setsCount: number; repsCount: number; orderIndex: number }[]
}

export function useMemberWorkoutAssignments(memberId: string | undefined) {
  return useQuery({
    queryKey: ['workout-assignments', memberId],
    queryFn: async () => (await apiClient.get<WorkoutAssignmentListItem[]>(`/api/workouts/assignments/member/${memberId}`)).data,
    enabled: !!memberId,
  })
}

interface AssignWorkoutTemplateInput {
  memberId: string
  workoutTemplateId: string
  startDate: string
  endDate?: string
  notes?: string
}

export function useAssignWorkoutTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AssignWorkoutTemplateInput) => (await apiClient.post<string>('/api/workouts/assignments', input)).data,
    onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: ['workout-assignments', variables.memberId] }),
  })
}

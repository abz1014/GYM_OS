import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface MyChallenge {
  id: string
  name: string
  description: string | null
  startDate: string
  endDate: string
  targetWorkoutCount: number
  joined: boolean
  isCompleted: boolean
  myWorkoutCount: number
}

export function useMyChallenges() {
  return useQuery({
    queryKey: ['portal', 'challenges'],
    queryFn: async () => (await apiClient.get<MyChallenge[]>('/api/me/challenges')).data,
    retry: false,
  })
}

function invalidateMyChallenges(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['portal', 'challenges'] })
  queryClient.invalidateQueries({ queryKey: ['portal', 'experience'] })
  queryClient.invalidateQueries({ queryKey: ['portal', 'achievements'] })
}

export function useJoinChallenge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (challengeId: string) => apiClient.post(`/api/me/challenges/${challengeId}/join`),
    onSuccess: () => invalidateMyChallenges(queryClient),
  })
}

export function useLeaveChallenge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (challengeId: string) => apiClient.post(`/api/me/challenges/${challengeId}/leave`),
    onSuccess: () => invalidateMyChallenges(queryClient),
  })
}

// Staff management (Experience.Manage) — list + create only, matching the backend's scope.
export interface ChallengeListItem {
  id: string
  name: string
  description: string | null
  branchId: string | null
  startDate: string
  endDate: string
  targetWorkoutCount: number
  participantCount: number
  completedCount: number
}

export function useChallengesList() {
  return useQuery({
    queryKey: ['challenges', 'list'],
    queryFn: async () => (await apiClient.get<ChallengeListItem[]>('/api/challenges')).data,
    retry: false,
  })
}

export interface CreateChallengeInput {
  name: string
  description: string | null
  branchId: string | null
  startDate: string
  endDate: string
  targetWorkoutCount: number
}

export function useCreateChallenge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateChallengeInput) => (await apiClient.post<string>('/api/challenges', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['challenges', 'list'] }),
  })
}

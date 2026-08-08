import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'
import type { CoachMessageSession } from '@/shared/components/coaching/SessionAttachment'

export interface PlateauRow {
  memberId: string
  memberName: string
  memberCode: string
  exerciseId: string
  exerciseName: string
  lastWeightKg: number | null
  suggestedNextWeightKg: number | null
  lastLoggedAt: string
}

export interface ComplianceRow {
  memberId: string
  memberName: string
  memberCode: string
  workoutAdherencePercent: number
  nutritionAdherencePercent: number | null
  lastWorkoutAt: string | null
  lastMealLoggedAt: string | null
}

export interface RiskRow {
  memberId: string
  memberName: string
  memberCode: string
  riskType: 'OvertrainingRisk' | 'StreakBreakImminent'
  reason: string
}

export interface MyClientRow {
  memberId: string
  memberName: string
  memberCode: string
  /** False once the assignment has ended: history stays readable, nothing new can be sent. */
  isActivePairing: boolean
  unreadFromMember: number
  lastMessageAt: string | null
  lastMessagePreview: string | null
}

export interface CoachMessage {
  id: string
  author: 'Member' | 'Trainer'
  body: string
  sentAt: string
  read: boolean
  workoutLogId: string | null
  /** Resolved server-side; null when the message is about nothing, or the workout was since deleted. */
  session: CoachMessageSession | null
}

export interface CoachConversation {
  memberId: string
  memberName: string
  /** The server's answer, not ours to infer — an ended pairing can be read but not written to. */
  canSend: boolean
  unreadCount: number
  hasOlder: boolean
  messages: CoachMessage[]
}

/** The acting trainer's own clients, ordered by who is waiting. See GetMyClientsQuery. */
export function useMyClients() {
  return useQuery({
    queryKey: ['coaching', 'my-clients'],
    queryFn: async () => (await apiClient.get<MyClientRow[]>('/api/coaching/clients')).data,
  })
}

export function useClientConversation(memberId: string | null) {
  return useQuery({
    queryKey: ['coaching', 'conversation', memberId],
    queryFn: async () =>
      (await apiClient.get<CoachConversation>(`/api/coaching/clients/${memberId}/messages`)).data,
    enabled: !!memberId,
  })
}

export function useMessageClient(memberId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ body, workoutLogId }: { body: string; workoutLogId: string | null }) =>
      (await apiClient.post<string>(`/api/coaching/clients/${memberId}/messages`, { body, workoutLogId })).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coaching', 'conversation', memberId] })
      // The roster carries the last message and its preview, so it is stale the moment one is sent.
      queryClient.invalidateQueries({ queryKey: ['coaching', 'my-clients'] })
    },
  })
}

/** Marks the member's messages read. Fired on opening a thread — see MyClientsPage. */
export function useMarkClientMessagesRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (memberId: string) => apiClient.post(`/api/coaching/clients/${memberId}/messages/read`),
    onSuccess: (_r, memberId) => {
      queryClient.invalidateQueries({ queryKey: ['coaching', 'conversation', memberId] })
      queryClient.invalidateQueries({ queryKey: ['coaching', 'my-clients'] })
    },
  })
}

export function useCoachingPlateaus() {
  return useQuery({
    queryKey: ['coaching', 'plateaus'],
    queryFn: async () => (await apiClient.get<PlateauRow[]>('/api/coaching/plateaus')).data,
  })
}

export function useCoachingCompliance() {
  return useQuery({
    queryKey: ['coaching', 'compliance'],
    queryFn: async () => (await apiClient.get<ComplianceRow[]>('/api/coaching/compliance')).data,
  })
}

export function useCoachingRisks() {
  return useQuery({
    queryKey: ['coaching', 'risks'],
    queryFn: async () => (await apiClient.get<RiskRow[]>('/api/coaching/risks')).data,
  })
}

export interface RebuildProjectionsResult {
  membersConsidered: number
  progressionsRebuilt: number
  masteryRowsRebuilt: number
  achievementsBackfilled: number
}

// The Slice 10 hardening safety net: recomputes MemberProgression/ExerciseMastery from their
// append-only sources and backfills any achievement a corrected value newly satisfies. Safe to run
// any time — idempotent, never touches XpTransaction/PersonalRecord/existing achievement rows.
export function useRebuildExperienceProjections() {
  return useMutation({
    mutationFn: async () => (await apiClient.post<RebuildProjectionsResult>('/api/experience/rebuild-projections')).data,
  })
}

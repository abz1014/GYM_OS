import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

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

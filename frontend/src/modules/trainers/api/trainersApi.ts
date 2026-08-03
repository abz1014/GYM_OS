import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface TrainerListItem {
  id: string
  fullName: string
  email: string
  specialties: string
  commissionRate: number
  isActive: boolean
  activeClientCount: number
  averageRating: number | null
}

export interface TrainerSchedule {
  id: string
  dayOfWeek: string
  startTime: string
  endTime: string
  isAvailable: boolean
}

export interface TrainerAssignment {
  id: string
  memberId: string
  memberName: string
  startDate: string
  endDate: string | null
  isActive: boolean
}

export interface TrainerRating {
  id: string
  memberId: string
  memberName: string
  score: number
  comment: string | null
  ratedAt: string
}

export interface CommissionRecord {
  id: string
  amount: number
  period: string
  status: string
}

export interface TrainerDetail {
  id: string
  firstName: string
  lastName: string
  email: string
  specialties: string
  commissionRate: number
  bio: string | null
  isActive: boolean
  branchId: string
  schedules: TrainerSchedule[]
  assignments: TrainerAssignment[]
  ratings: TrainerRating[]
  commissionRecords: CommissionRecord[]
  totalCommissionEarned: number
}

export function useTrainersList(branchId?: string | null) {
  return useQuery({
    queryKey: ['trainers', branchId],
    queryFn: async () => (await apiClient.get<TrainerListItem[]>('/api/trainers', { params: { branchId } })).data,
  })
}

export function useTrainer(id: string | undefined) {
  return useQuery({
    queryKey: ['trainer', id],
    queryFn: async () => (await apiClient.get<TrainerDetail>(`/api/trainers/${id}`)).data,
    enabled: !!id,
  })
}

interface CreateTrainerInput {
  firstName: string
  lastName: string
  email: string
  branchId: string
  specialties: string
  commissionRate: number
  bio?: string
}

export function useCreateTrainer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateTrainerInput) =>
      (await apiClient.post<{ trainerId: string; temporaryPassword: string }>('/api/trainers', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['trainers'] }),
  })
}

export function useAssignClient(trainerId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (memberId: string) => apiClient.post(`/api/trainers/${trainerId}/assignments`, { memberId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['trainer', trainerId] }),
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface NotificationTemplate {
  id: string
  code: string
  category: string
  channel: string
  subject: string
  bodyTemplate: string
  isActive: boolean
}

export interface ScheduledNotification {
  id: string
  templateCode: string
  category: string
  channel: string
  recipientName: string | null
  scheduledFor: string
  status: string
}

export interface NotificationLog {
  id: string
  channel: string
  recipientAddress: string
  subject: string
  body: string
  sentAt: string
  success: boolean
}

export interface TriggerChecksResult {
  scheduledCount: number
  dispatchedCount: number
}

export function useNotificationTemplates() {
  return useQuery({
    queryKey: ['notification-templates'],
    queryFn: async () => (await apiClient.get<NotificationTemplate[]>('/api/notifications/templates')).data,
  })
}

export function useUpdateNotificationTemplate(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { subject: string; bodyTemplate: string; isActive: boolean }) =>
      apiClient.put(`/api/notifications/templates/${id}`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notification-templates'] }),
  })
}

export function useScheduledNotifications(status?: string) {
  return useQuery({
    queryKey: ['scheduled-notifications', status],
    queryFn: async () =>
      (await apiClient.get<ScheduledNotification[]>('/api/notifications/scheduled', { params: { status } })).data,
  })
}

export function useNotificationLogs() {
  return useQuery({
    queryKey: ['notification-logs'],
    queryFn: async () => (await apiClient.get<NotificationLog[]>('/api/notifications/logs')).data,
  })
}

export function useTriggerNotificationChecks() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => (await apiClient.post<TriggerChecksResult>('/api/notifications/run-checks')).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['scheduled-notifications'] })
      queryClient.invalidateQueries({ queryKey: ['notification-logs'] })
    },
  })
}

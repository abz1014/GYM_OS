import { useEffect } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'

import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'

/** Joins the current branch's SignalR group and invalidates the dashboard query on any live activity event (check-in, payment). */
export function useDashboardHub() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const branchId = useUiStore((s) => s.selectedBranchId)
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!accessToken || !branchId) {
      return
    }

    const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/dashboard?access_token=${accessToken}`)
      .withAutomaticReconnect()
      .build()

    connection.on('activity', () => {
      queryClient.invalidateQueries({ queryKey: ['dashboard-summary', branchId] })
    })

    connection
      .start()
      .then(() => connection.invoke('JoinBranchGroup', branchId))
      .catch(() => {
        // Non-fatal — the dashboard still works via polling (refetchInterval); live push is a bonus.
      })

    return () => {
      connection.stop()
    }
  }, [accessToken, branchId, queryClient])
}

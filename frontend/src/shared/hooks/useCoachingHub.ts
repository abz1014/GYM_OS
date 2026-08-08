import { useEffect } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'

import { useAuthStore } from '@/stores/authStore'

/**
 * Keeps a coaching conversation live, so a message appears on the other side without a refresh.
 *
 * Unlike useDashboardHub this joins nothing — there is no group id to send, because the server puts
 * each connection in its own user's group from the token and will not take one from the caller. See
 * CoachingHub for why. Nothing but a bare signal comes down the socket either: the handler
 * invalidates and the ordinary authorised endpoints hand over the words.
 *
 * Both sides use this same hook. The member portal cares about the thread and the unread badge on
 * their home screen; the trainer console cares about the thread and the roster ordering. Rather than
 * have each caller remember which keys those are, everything the event can possibly have changed is
 * invalidated here — they are four cached queries, and getting one wrong is a stale badge nobody can
 * explain.
 */
export function useCoachingHub() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!accessToken) {
      return
    }

    const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/coaching?access_token=${accessToken}`)
      .withAutomaticReconnect()
      .build()

    connection.on('conversationChanged', () => {
      // Trainer side: the open thread and the roster that orders by who is waiting.
      queryClient.invalidateQueries({ queryKey: ['coaching', 'conversation'] })
      queryClient.invalidateQueries({ queryKey: ['coaching', 'my-clients'] })
      // Member side: their view of the thread, and the home query the unread badge rides on.
      // Keys are the portal's own — ['portal', ...] — not the hook names.
      queryClient.invalidateQueries({ queryKey: ['portal', 'coach'] })
      queryClient.invalidateQueries({ queryKey: ['portal', 'today'] })
    })

    connection.start().catch(() => {
      // Non-fatal by design, exactly like the dashboard hub. Every screen this feeds still works on
      // its ordinary fetch; live delivery is an improvement on top, not a dependency. A member on a
      // flaky connection gets their coach's reply a beat later rather than an error.
    })

    return () => {
      connection.stop()
    }
  }, [accessToken, queryClient])
}

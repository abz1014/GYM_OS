import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

/**
 * Failed auto-renewals — the list a gym chases every week, and the one the console could not show.
 *
 * THE GAP. RecurringBillingJob has always written a full dunning record: who declined, how many
 * retries have burned, the gateway's reason, when the next attempt falls, and whether the member has
 * been suspended for non-payment. Nothing could read it. Outside the job itself the only reference
 * to RecurringBillingAttempt in the whole Application layer was the DbSet declaration — so the
 * owner could see that revenue had not arrived, but never who to ring about it.
 *
 * Deliberately its own file rather than more weight on billingApi: this is a different question
 * ("who owes us and why did the card fail") from invoicing, and it is the one an owner opens on a
 * Monday morning.
 */

export type DunningStatus = 'Pending' | 'Succeeded' | 'Abandoned'

export interface DunningAttempt {
  id: string
  memberId: string
  memberName: string
  invoiceId: string
  invoiceNumber: string
  amount: number
  currency: string
  failedAttempts: number
  /** The retry budget from BillingRetryPolicy, so "2 of 4" is a real fraction, not a guess. */
  maxAttempts: number
  lastFailureReason: string | null
  nextAttemptDate: string
  lastAttemptDate: string | null
  status: DunningStatus
  /** True once retries were exhausted and access was suspended — the member is locked out today. */
  membershipSuspended: boolean
}

export function useDunningAttempts() {
  return useQuery({
    queryKey: ['billing', 'dunning'],
    queryFn: async () => (await apiClient.get<DunningAttempt[]>('/api/billing/dunning')).data,
  })
}

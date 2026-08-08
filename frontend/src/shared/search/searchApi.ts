import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface SearchHit {
  id: string
  title: string
  subtitle: string | null
  /** Resolved server-side, so it is always a route that exists. */
  route: string
}

export interface GlobalSearchResult {
  members: SearchHit[]
  invoices: SearchHit[]
  classes: SearchHit[]
}

const EMPTY: GlobalSearchResult = { members: [], invoices: [], classes: [] }

/**
 * The palette's search.
 *
 * No `enabled: hasPermission(...)` here, unlike every other cross-module hook in this app — and that
 * is deliberate rather than an oversight. `/api/search` carries no permission of its own and filters
 * each group against the caller's module permissions server-side, so there is nothing for the client
 * to gate: a Nutritionist calling this gets members and two empty arrays, never a 403. Gating it here
 * would mean re-deriving the server's rule in the client and getting it wrong the day it changes.
 *
 * The two-character floor mirrors the server's, so a one-character term costs no request at all
 * rather than a round trip that is guaranteed to come back empty.
 */
export function useGlobalSearch(term: string) {
  const trimmed = term.trim()

  return useQuery({
    queryKey: ['search', trimmed],
    queryFn: async () => (await apiClient.get<GlobalSearchResult>('/api/search', { params: { q: trimmed } })).data,
    enabled: trimmed.length >= 2,
    // A palette is typed at, so results churn fast and go stale the moment someone edits a record.
    // Short-lived cache keeps backspacing instant without serving a minute-old answer.
    staleTime: 15_000,
    placeholderData: (previous) => previous,
  })
}

export function isEmptyResult(result: GlobalSearchResult | undefined): boolean {
  const r = result ?? EMPTY
  return r.members.length === 0 && r.invoices.length === 0 && r.classes.length === 0
}

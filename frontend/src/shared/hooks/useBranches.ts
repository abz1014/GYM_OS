import { useQuery } from '@tanstack/react-query'

import { apiClient } from '@/lib/apiClient'

export interface Branch {
  id: string
  name: string
  city: string
  currency: string
}

/**
 * The branches this user can reach — the single most load-bearing request in the staff console, and
 * the one whose failure used to be invisible.
 *
 * Almost every staff query is gated on `enabled: !!branchId`, and the only thing that ever sets
 * `selectedBranchId` is BranchSwitcher's effect, which runs off this query's data. So when
 * /api/branches failed, nothing set the branch, nothing became enabled, and every gated query sat in
 * `pending` — not `error` — forever. Dashboard, Front desk and CRM all showed permanent skeletons
 * with no error, no retry and no explanation, because from react-query's point of view nobody had
 * asked a question yet.
 *
 * Defined once here so BranchSwitcher and useBranchScope share the cache entry AND the status, and
 * so every caller that needs to distinguish "still asking" from "asked and failed" can.
 */
export function useBranchesQuery() {
  return useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await apiClient.get<Branch[]>('/api/branches')).data,
  })
}

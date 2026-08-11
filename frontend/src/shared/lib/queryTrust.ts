/**
 * Whether a query's answer can be believed.
 *
 * Every screen in this product decides something from query results, and a surprising number of
 * those decisions are about ABSENCE: no risk flag, no last visit, no invoice, an empty list. Absence
 * is not neutral — staff read a clean screen as "nothing to report" and act on it — so a query that
 * failed does not withhold information, it asserts the opposite of the truth.
 *
 * The obvious check, `isError`, catches the least likely of the three ways that happens:
 *
 * - isError         failed outright with nothing cached. The obvious one.
 * - isRefetchError  failed while refreshing, so what is on screen is real but from an earlier
 *                   minute, and nothing on the screen says so.
 * - isPaused        React Query could not reach the network. THIS IS THE ONE THAT MATTERS. A paused
 *                   query reports status 'pending' and isError FALSE for as long as the outage
 *                   lasts, so an isError-only check stays silent through the entire thing — and a
 *                   dropped connection is far likelier than a 500. Found by probing live query state
 *                   after breaking an endpoint: status 'pending', fetchStatus 'paused', isError
 *                   false, with the screen calmly showing partial data.
 *
 * A query that was never enabled is NOT untrustworthy. A disabled query sits at fetchStatus 'idle',
 * so it fails all three tests on its own — a screen that does not ask a question is not being lied
 * to by the answer.
 */
export interface TrustableQuery {
  isError: boolean
  isRefetchError: boolean
  isPaused: boolean
}

/** True when this query's answer must not be presented as complete. */
export function isStale(query: TrustableQuery): boolean {
  return query.isError || query.isRefetchError || query.isPaused
}

/**
 * True when ANY of these answers must not be presented as complete.
 *
 * Pass every query whose result feeds the conclusion on screen. Falsy entries are ignored, so a
 * caller can inline a permission check — `canSeeBilling && invoicesQuery` — without a branch.
 */
export function anyStale(...queries: (TrustableQuery | false | null | undefined)[]): boolean {
  return queries.some((q) => q && isStale(q))
}

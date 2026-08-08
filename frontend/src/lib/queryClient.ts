import { QueryClient } from '@tanstack/react-query'

/**
 * There is deliberately no QueryCache or MutationCache `onError` here, and the reasoning is worth
 * keeping because the absence looks like an oversight and is cited as one elsewhere (see
 * equipmentApi's useUpdateAssetStatus).
 *
 * A global MUTATION handler cannot tell a handled failure from an unhandled one. query-core calls
 * `mutationCache.config.onError` first and unconditionally, then the mutation's own `onError`, and
 * then — from inside MutationObserver, off a private field the cache callback has no access to —
 * the `onError` passed per call to `.mutate(vars, { onError })`. That last form is the one the
 * careful call sites in this app use, so the cache handler would be blind to precisely the code
 * that already gets this right: ~119 mutations against ~94 local error handlers, meaning a global
 * toast would fire a second, vaguer toast on top of most of the specific ones ("Couldn't send that
 * message." followed by "Something went wrong") and add nothing to the rest that a local handler
 * with real wording wouldn't say better. Gating it on `meta` would fix the double-toast, but an
 * opt-in global handler is just a local handler with worse copy and an extra indirection — the
 * call sites still have to be visited one by one, which is the actual work.
 *
 * A global QUERY handler is worse still: half the console polls (the dashboard summary every 30s,
 * overdue invoices and the in-building count every 60s), so a toast on query error would fire
 * repeatedly, in the background, for screens the user is not looking at. Failed reads are shown
 * where the number would have been — that is the whole point of the error states this file's
 * absence of a handler pushes onto the components — not in a corner of the screen.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

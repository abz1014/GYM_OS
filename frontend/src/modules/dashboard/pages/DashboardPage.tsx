import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'

import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { useDashboardHub } from '@/shared/hooks/useDashboardHub'
import { resolveLandingRoute } from '@/shared/nav/landingRoute'
import {
  summarizeOverdueInvoices,
  useAtRiskMembers,
  useBranchScope,
  useDashboardSummary,
  useOverdueInvoices,
  useStillCheckedInCount,
} from '@/modules/dashboard/api/dashboardApi'
import { StatTile } from '@/shared/components/console'
import { CheckInsByHourPanel } from '@/modules/dashboard/components/CheckInsByHourPanel'
import { NeedsYouPanel } from '@/modules/dashboard/components/NeedsYouPanel'

const dateHeadingFormat = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric' })

/**
 * How long ago the numbers on screen were last true.
 *
 * This is what remains of the mockup's "Live · updated 12s ago" chip. The timestamp is real —
 * useDashboardSummary polls every 30s and useDashboardHub invalidates it on any live branch
 * activity, and dataUpdatedAt records when the answer actually landed. The word "Live" and its
 * pulsing dot are gone: the hub swallows its own connection failure by design (the dashboard still
 * works on polling) and exposes no connection state, so a socket that never opened would look
 * exactly like one that did. "Updated 12s ago" is true either way.
 */
function useUpdatedAgo(updatedAt: number): string | null {
  const [, setTick] = useState(0)

  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 5_000)
    return () => clearInterval(id)
  }, [])

  if (!updatedAt) {
    return null
  }

  const seconds = Math.max(0, Math.round((Date.now() - updatedAt) / 1000))
  if (seconds < 10) return 'just now'
  if (seconds < 60) return `${seconds}s ago`

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  return `${Math.floor(minutes / 60)}h ago`
}

function money(amount: number, currency: string) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
}

function plural(count: number, singular: string, pluralForm = `${singular}s`) {
  return count === 1 ? singular : pluralForm
}

/**
 * The staff console's opening screen: what today looks like, and what is waiting on someone.
 *
 * It used to be ten identical stat tiles — every number the summary endpoint returned, at equal
 * weight, with nothing to do about any of them. The redesign splits that into three questions: four
 * KPIs for the state of the business, a chart for the shape of the day, and a queue of things that
 * need a person. Nothing was invented to fill it; several mockup elements were dropped instead:
 *
 *  - MRR. No endpoint in this system reports recurring or contracted revenue — /api/reports/revenue
 *    is realised payments by month, which is a different number, and the summary's revenue figure is
 *    today's takings. Today's revenue is what this shows, under its own name.
 *  - The trend-delta chips on every KPI, and the Active-members sparkline. Both need a previous
 *    period, and the summary is a single snapshot. See StatTile.
 *  - The top bar's search box, ⌘K hint, date-range chip and notification bell. No command palette
 *    exists (nothing in the app binds a global shortcut), the summary takes no date range, and
 *    search/notifications belong to the shell's Topbar, not to this page.
 *  - The "New member" button. Member creation is a dialog owned by MembersListPage with no way to
 *    open it from a URL, so the button could only have dropped the user on the members list to press
 *    a second button.
 */
export default function DashboardPage() {
  const hasPermission = useAuthStore((s) => s.hasPermission)

  const summaryQuery = useDashboardSummary()
  useDashboardHub()

  const { branch, branchCount } = useBranchScope()
  const stillCheckedIn = useStillCheckedInCount()
  const overdue = useOverdueInvoices()
  const atRisk = useAtRiskMembers()

  const updatedAgo = useUpdatedAgo(summaryQuery.dataUpdatedAt)
  const summary = summaryQuery.data

  // HomeRedirect only guards "/" and unknown routes — a direct hit on /dashboard (a stale
  // bookmark, browser history from before a role change, a hand-typed URL) bypasses that and
  // used to hit /api/dashboard/summary, 403, and then sit on the loading skeleton forever since
  // isLoading/!data never distinguished "still fetching" from "failed for good". Same fix
  // HomeRedirect already uses: send the user to whatever their permissions actually allow.
  if (!hasPermission('dashboard.view')) {
    return <Navigate to={resolveLandingRoute(hasPermission)} replace />
  }

  const heading = dateHeadingFormat.format(new Date())

  if (summaryQuery.isError) {
    return (
      <div className="space-y-2">
        <h1 className="font-display text-3xl font-black tracking-tight">{heading}</h1>
        <p className="text-sm text-muted-foreground">
          Something went wrong loading the dashboard. Try refreshing the page.
        </p>
      </div>
    )
  }

  const overdueSummary = summarizeOverdueInvoices(overdue.data)

  /*
   * One line of state, composed only of clauses whose count is genuinely above zero — a dashboard
   * that opens with "0 assets down" trains people to stop reading it. If every source is quiet (or
   * the user can't see any of them), the line isn't rendered at all rather than claiming all-clear
   * on behalf of data this user may not have permission to read.
   */
  const clauses: string[] = []
  if (stillCheckedIn.data) {
    clauses.push(`${stillCheckedIn.data} still checked in`)
  }
  if (overdueSummary && overdueSummary.count > 0) {
    clauses.push(`${overdueSummary.count} ${plural(overdueSummary.count, 'invoice')} need chasing`)
  }
  if (summary && summary.equipmentAlertsCount > 0) {
    clauses.push(`${summary.equipmentAlertsCount} ${plural(summary.equipmentAlertsCount, 'asset')} down`)
  }
  if (summary && summary.trainerScheduleTodayCount > 0) {
    clauses.push(`${summary.trainerScheduleTodayCount} ${plural(summary.trainerScheduleTodayCount, 'trainer')} scheduled`)
  }

  // Tenant-wide figures have to say so, but only where it changes the meaning — on a single-branch
  // tenant "all branches" is noise, on a three-branch one it's the difference between a number you
  // can act on and one you can't.
  const allBranchesNote = branchCount > 1 ? ' · all branches' : ''

  // Derived from the rows themselves rather than repeating ChurnRiskPolicy's threshold in the
  // frontend, where it would quietly go stale the day the backend constant changes.
  const quietDays = atRisk.data?.length ? Math.min(...atRisk.data.map((m) => m.daysSinceLastVisit)) : null

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          {branch && (
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{branch.name}</p>
          )}
          <h1 className="font-display text-3xl leading-tight font-black tracking-tight">{heading}</h1>
          {summaryQuery.isLoading ? (
            <Skeleton className="mt-2 h-5 w-72" />
          ) : (
            clauses.length > 0 && <p className="mt-1 text-sm text-muted-foreground">{clauses.join(' · ')}</p>
          )}
        </div>

        {updatedAgo && (
          <p className="flex items-center gap-2 text-xs text-muted-foreground">
            <span
              className={cn('size-1.5 rounded-full bg-success', summaryQuery.isFetching && 'animate-pulse')}
              aria-hidden
            />
            Updated {updatedAgo}
          </p>
        )}
      </header>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {!summary || !branch ? (
          Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[132px] w-full rounded-2xl" />)
        ) : (
          <>
            <StatTile
              label="Active members"
              value={summary.activeMembersCount.toLocaleString()}
              caption={
                summary.newMembersThisMonthCount > 0
                  ? `+${summary.newMembersThisMonthCount} joined this month`
                  : undefined
              }
              captionTone="success"
            />

            <StatTile
              label="Revenue today"
              value={money(summary.todayRevenue, branch.currency)}
              caption={
                summary.todayCashCollected > 0
                  ? `${money(summary.todayCashCollected, branch.currency)} of it in cash`
                  : undefined
              }
            />

            {/*
              Both cards below are gated on the permission their endpoint needs rather than rendered
              empty: a receptionist without reports.view or billing.view simply gets a shorter row,
              instead of a tile that says nothing next to three that say something.

              Their captions are plain text on purpose. The redesign asked for captions that link
              through to the filtered view they describe ("No visit in 21 days →"), and no such view
              is addressable: the at-risk list is a tab inside /reports and the invoice list filters
              by status in local state — no page in this app reads a filter out of the URL. The
              matching rows in "Needs you" link to the module, which is a promise the app can keep.
            */}
            {hasPermission('reports.view') &&
              (atRisk.isLoading ? (
                <Skeleton className="h-[132px] w-full rounded-2xl" />
              ) : (
                <StatTile
                  label="At risk"
                  value={(atRisk.data?.length ?? 0).toLocaleString()}
                  caption={quietDays !== null ? `No visit in ${quietDays}+ days${allBranchesNote}` : undefined}
                  captionTone="warning"
                />
              ))}

            {hasPermission('billing.view') &&
              (overdue.isLoading ? (
                <Skeleton className="h-[132px] w-full rounded-2xl" />
              ) : (
                <StatTile
                  label="Overdue invoices"
                  value={(overdueSummary?.count ?? 0).toLocaleString()}
                  caption={
                    overdueSummary && overdueSummary.count > 0
                      ? `${
                          overdueSummary.outstanding
                            ? `${money(overdueSummary.outstanding.amount, overdueSummary.outstanding.currency)} outstanding`
                            : 'Unpaid past due'
                        }${allBranchesNote}`
                      : undefined
                  }
                  captionTone="destructive"
                />
              ))}
          </>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        {/* The chart needs attendance.view; without it the queue takes the full width rather than
            leaving a hole where a panel this user can never load would have been. */}
        {hasPermission('attendance.view') && (
          <div className="lg:col-span-2">
            <CheckInsByHourPanel />
          </div>
        )}
        <div className={cn(!hasPermission('attendance.view') && 'lg:col-span-3')}>
          <NeedsYouPanel summary={summary} />
        </div>
      </div>
    </div>
  )
}

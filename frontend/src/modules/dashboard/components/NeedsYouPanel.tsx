import { Link } from 'react-router-dom'
import { ChevronRight, CloudOff } from 'lucide-react'

import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import {
  summarizeOverdueInvoices,
  useLeadPipeline,
  useOverdueInvoices,
  type DashboardSummary,
} from '@/modules/dashboard/api/dashboardApi'
import { isStale } from '@/shared/lib/queryTrust'

type QueueTone = 'critical' | 'warning' | 'neutral'

/**
 * A 3px inset rail on a tinted ground — the same rail the KPI tiles above carry, so severity reads
 * the same way in both halves of the screen. It replaces the 4px left BORDER these rows used to
 * have: a border changes the box's size, which pushed the tinted rows' text 4px right of the
 * untinted ones, and an inset shadow doesn't.
 *
 * The grounds are the console's three tinted surfaces rather than an alpha of the tone. `warning/5`
 * over white is a different colour from `#FDF6EC` and the two would not have matched the tabs and
 * badges that already use these exact values elsewhere in the light palette.
 *
 * The neutral rail has no `rail-*` utility because there is no neutral SEVERITY — it is the absence
 * of one, drawn just heavy enough to keep the row's leading edge aligned with the two above it.
 */
const TONE_CLASSES: Record<QueueTone, string> = {
  critical: 'rail-destructive bg-[#FDF2F2]',
  warning: 'rail-warning bg-[#FDF6EC]',
  neutral: 'bg-[#F4F4F0] shadow-[inset_3px_0_0_#8A8A80]',
}

/**
 * Hover has to be per-tone now that the ground is: a single `hover:bg-accent` would wash a critical
 * row back to neutral grey on the way to clicking it. Only rows that actually go somewhere get one.
 */
const TONE_HOVER_CLASSES: Record<QueueTone, string> = {
  critical: 'transition-colors hover:bg-[#FAE8E8]',
  warning: 'transition-colors hover:bg-[#F9EDDC]',
  neutral: 'transition-colors hover:bg-[#ECECE6]',
}

interface QueueRow {
  key: string
  tone: QueueTone
  headline: string
  detail?: string
  /** Only set when the destination both exists as a route and is open to this user. */
  to?: string
}

function plural(count: number, singular: string, pluralForm = `${singular}s`) {
  return count === 1 ? singular : pluralForm
}

/**
 * The redesign's "Needs you" queue, replacing the wall of ten stat tiles this page used to be.
 *
 * The rule for this panel is that every row is a live query and a row with a count of zero does not
 * render — so an empty queue is a real statement about the gym, not an empty widget. That rule also
 * removed two of the mockup's rows outright:
 *
 *  - "12 leads untouched 48h". Nothing records when a lead was last touched: LeadListItem carries
 *    createdAt and score only, activities live on the lead detail, and no endpoint aggregates them.
 *    The nearest honest fact — leads still sitting at the New stage — is what this renders instead,
 *    and it says exactly that rather than implying a neglect clock nobody is running.
 *  - The per-row colour on details like "Work order open 4 days · no engineer". Assignment and age
 *    are on the work-order detail, not on any count this dashboard can reach in one request.
 *
 * The mockup's "Open action queue" footer link is gone with them: there is no action-queue route,
 * and every row already goes to the module that owns the work.
 *
 * The uplift pass changed the rows' surface and nothing else: the 4px border became the KPI tiles'
 * inset rail on a tinted ground, which is tighter and lets six rows sit where three did. Every
 * headline, every detail line and every destination is the same, and the four rows that carry no
 * detail still carry none — the facts that would fill them ("N have no renewal booked", "untouched
 * for 5+ days", per-asset age) all need tracking this product does not do.
 */
export function NeedsYouPanel({ summary }: { summary: DashboardSummary | undefined }) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const overdue = useOverdueInvoices()
  const leads = useLeadPipeline()

  const rows: QueueRow[] = []

  const overdueSummary = summarizeOverdueInvoices(overdue.data)
  if (overdueSummary && overdueSummary.count > 0) {
    const detail = [
      overdueSummary.outstanding
        ? `${new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: overdueSummary.outstanding.currency,
          }).format(overdueSummary.outstanding.amount)} outstanding`
        : null,
      overdueSummary.oldestDays !== null ? `oldest ${overdueSummary.oldestDays} days` : null,
    ]
      .filter(Boolean)
      .join(' · ')

    rows.push({
      key: 'invoices',
      tone: 'critical',
      headline: `${overdueSummary.count} ${plural(overdueSummary.count, 'invoice')} overdue`,
      detail: detail || undefined,
      // The query only runs for someone holding billing.view, so reaching this row already implies
      // the link is open to them.
      to: '/billing',
    })
  }

  if (summary && summary.expiringMembershipsNext7DaysCount > 0) {
    rows.push({
      key: 'expiring',
      tone: 'warning',
      headline: `${summary.expiringMembershipsNext7DaysCount} ${plural(summary.expiringMembershipsNext7DaysCount, 'membership')} expire within 7 days`,
      // No link: nothing in the app lists memberships by expiry. The members list filters by member
      // status only, and no page reads a filter out of the URL, so there is no filtered view to open.
      // The mockup's "18 have not been contacted" needs outreach tracking that doesn't exist either.
    })
  }

  if (leads.data && leads.data.leadCount > 0) {
    rows.push({
      key: 'leads',
      tone: 'warning',
      headline: `${leads.data.leadCount} new ${plural(leads.data.leadCount, 'lead')} waiting`,
      detail: 'Still at the New stage',
      to: hasPermission('crm.view') ? '/crm' : undefined,
    })
  }

  if (summary && summary.equipmentAlertsCount > 0) {
    rows.push({
      key: 'equipment',
      tone: 'neutral',
      headline: `${summary.equipmentAlertsCount} ${plural(summary.equipmentAlertsCount, 'asset')} out of service or under maintenance`,
      to: hasPermission('equipment.view') ? '/equipment' : undefined,
    })
  }

  if (summary && summary.maintenanceRemindersCount > 0) {
    rows.push({
      key: 'work-orders',
      tone: 'neutral',
      headline: `${summary.maintenanceRemindersCount} ${plural(summary.maintenanceRemindersCount, 'work order')} past the scheduled date`,
      to: hasPermission('maintenance.view') ? '/maintenance' : undefined,
    })
  }

  if (summary && summary.inventoryAlertsCount > 0) {
    rows.push({
      key: 'inventory',
      tone: 'neutral',
      headline: `${summary.inventoryAlertsCount} stock ${plural(summary.inventoryAlertsCount, 'item')} at or below reorder level`,
      to: hasPermission('inventory.view') ? '/inventory' : undefined,
    })
  }

  const isLoading = !summary || overdue.isLoading || leads.isLoading

  /*
   * Which sources this queue is missing, by name.
   *
   * A row only renders when its count is above zero, which is what makes an empty queue meaningful —
   * and which is exactly what made a failure indistinguishable from calm. A dead billing call ended
   * with no invoice row, and if the rest of the gym happened to be quiet the panel printed "Nothing
   * in the queue right now." over an unknown number of overdue invoices. So the failures are named,
   * the all-clear is withheld whenever there is one, and any rows that DID load are still shown —
   * "we're missing billing" is more useful than throwing away the two facts we have.
   *
   * `summary` is not in this list: DashboardPage renders an error for the whole page when the
   * summary call fails, so by the time this panel is mounted with data, that one has succeeded.
   * Nor are queries the user has no permission for — those are disabled, never error, and their
   * absence is correct rather than a fault.
   */
  const missing: string[] = []
  // `&& !data`: the overdue query polls every 60s, and a failed poll on top of a good answer leaves
  // the row on screen — saying the queue is missing invoices directly above the invoice row would be
  // the panel contradicting itself.
  if (isStale(overdue) && !overdue.data) missing.push('overdue invoices')
  if (isStale(leads) && !leads.data) missing.push('new leads')

  const retryMissing = () => {
    if (isStale(overdue)) void overdue.refetch()
    if (isStale(leads)) void leads.refetch()
  }

  return (
    <section className="flex flex-col rounded-3xl border border-border bg-card p-6 edge-light-soft">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-display text-lg font-bold tracking-tight">Needs you</h2>
        {/* No badge while anything is missing: the number would be a count of the queue, and this is
            a count of the part of it that loaded. */}
        {rows.length > 0 && missing.length === 0 && (
          <span className="flex min-w-6 items-center justify-center rounded-lg bg-foreground px-2 py-0.5 font-display text-xs font-bold text-background tabular-nums">
            {rows.length}
          </span>
        )}
      </div>

      {isLoading ? (
        <div className="mt-4 space-y-2">
          {/* Skeletons match the SHAPE of what is loading — same 14px radius, same row height. */}
          <Skeleton className="h-[68px] w-full rounded-[14px]" />
          <Skeleton className="h-[68px] w-full rounded-[14px]" />
          <Skeleton className="h-[68px] w-full rounded-[14px]" />
        </div>
      ) : rows.length === 0 && missing.length === 0 ? (
        <p className="mt-4 text-sm text-muted-foreground">Nothing in the queue right now.</p>
      ) : (
        <ul className="mt-4 space-y-2">
          {rows.map((row) => {
            const className = cn(
              'flex items-center gap-2.5 rounded-[14px] py-3.5 pr-4 pl-5',
              TONE_CLASSES[row.tone],
              row.to && TONE_HOVER_CLASSES[row.tone]
            )
            const content = (
              <>
                <span className="min-w-0 flex-1">
                  <span className="block font-display font-bold tracking-tight tabular-nums">{row.headline}</span>
                  {row.detail && <span className="mt-0.5 block text-sm text-muted-foreground">{row.detail}</span>}
                </span>
                {/* The chevron is the only thing distinguishing a row you can open from one you can't,
                    now that the border is gone. Four of these rows deliberately go nowhere. */}
                {row.to && <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />}
              </>
            )

            return (
              <li key={row.key}>
                {row.to ? (
                  <Link to={row.to} className={className}>
                    {content}
                  </Link>
                ) : (
                  <div className={className}>{content}</div>
                )}
              </li>
            )
          })}
        </ul>
      )}

      {!isLoading && missing.length > 0 && (
        <p className="mt-4 flex items-start gap-2 text-sm text-muted-foreground">
          <CloudOff className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            We couldn't load {missing.join(' or ')}, so this queue is incomplete.{' '}
            <button
              type="button"
              onClick={retryMissing}
              disabled={overdue.isFetching || leads.isFetching}
              className="font-medium text-foreground underline underline-offset-2 disabled:opacity-50"
            >
              {overdue.isFetching || leads.isFetching ? 'Trying…' : 'Try again'}
            </button>
          </span>
        </p>
      )}
    </section>
  )
}

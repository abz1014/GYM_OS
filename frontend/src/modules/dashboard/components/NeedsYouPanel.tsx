import { Link } from 'react-router-dom'

import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import {
  summarizeOverdueInvoices,
  useLeadPipeline,
  useOverdueInvoices,
  type DashboardSummary,
} from '@/modules/dashboard/api/dashboardApi'

type QueueTone = 'critical' | 'warning' | 'neutral'

const TONE_CLASSES: Record<QueueTone, string> = {
  critical: 'border-l-destructive bg-destructive/5',
  warning: 'border-l-warning bg-warning/5',
  neutral: 'border-l-muted-foreground/40 bg-muted/50',
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

  return (
    <section className="flex flex-col rounded-3xl border border-border bg-card p-6 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-display text-lg font-bold tracking-tight">Needs you</h2>
        {rows.length > 0 && (
          <span className="flex min-w-6 items-center justify-center rounded-lg bg-foreground px-2 py-0.5 font-display text-xs font-bold text-background tabular-nums">
            {rows.length}
          </span>
        )}
      </div>

      {isLoading ? (
        <div className="mt-4 space-y-3">
          <Skeleton className="h-[72px] w-full rounded-2xl" />
          <Skeleton className="h-[72px] w-full rounded-2xl" />
          <Skeleton className="h-[72px] w-full rounded-2xl" />
        </div>
      ) : rows.length === 0 ? (
        <p className="mt-4 text-sm text-muted-foreground">Nothing in the queue right now.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {rows.map((row) => {
            const className = cn(
              'block rounded-2xl border border-border border-l-4 p-4',
              TONE_CLASSES[row.tone],
              row.to && 'transition-colors hover:bg-accent'
            )
            const content = (
              <>
                <p className="font-display font-bold tracking-tight tabular-nums">{row.headline}</p>
                {row.detail && <p className="mt-0.5 text-sm text-muted-foreground">{row.detail}</p>}
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
    </section>
  )
}

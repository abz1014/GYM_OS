import { useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { Pagination } from '@/shared/components/Pagination'
import { FilterTabs, ListEmpty, ListError, ListSkeleton, PageHeader, SEVERITY_ROW, type FilterTab } from '@/shared/components/console'
import { useInvoicesList, type InvoiceStatus } from '@/modules/billing/api/billingApi'
import { CreateInvoiceDialog } from '@/modules/billing/components/CreateInvoiceDialog'

const STATUS_VARIANT: Record<InvoiceStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Draft: 'outline',
  Issued: 'secondary',
  PartiallyPaid: 'secondary',
  Paid: 'default',
  Overdue: 'destructive',
  Cancelled: 'outline',
  Refunded: 'outline',
}

/**
 * Every status, not a curated subset. Filtering to a state the list can contain but the tab row
 * omits is the kind of gap that makes staff distrust the filter and fall back to scrolling.
 */
const INVOICE_STATUSES: readonly InvoiceStatus[] = [
  'Draft',
  'Issued',
  'PartiallyPaid',
  'Paid',
  'Overdue',
  'Cancelled',
  'Refunded',
]

const currency = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

/** "PartiallyPaid" is a C# enum name, not something to put in front of a person. */
const statusLabel = (status: InvoiceStatus) => (status === 'PartiallyPaid' ? 'Part paid' : status)

/**
 * Uppercase eyebrow column headers — the console's panel vocabulary, shared with every detail route.
 * Lives here rather than in `components/ui/table` because that primitive is shadcn-generated and is
 * shared with surfaces that are not console panels.
 */
const COLUMN_HEAD = 'text-[10px] font-bold tracking-[0.13em] text-muted-foreground uppercase'

/**
 * The severity rail, reappearing from the dashboard KPIs on exactly the rows that are escalating.
 *
 * Drawn first here, but it now lives in `shared/components/console` because Maintenance, Inventory
 * and Equipment carry the same signal — a hex copied into four list modules is a hex that drifts.
 */
const OVERDUE_ROW = SEVERITY_ROW.destructive

/**
 * The console's list template, and the pair this page forms with InvoiceDetailPage is the one the
 * other twelve list/detail modules are meant to copy — Equipment, Maintenance, Inventory, CRM,
 * Trainers, Memberships, Classes, Workouts, Nutrition, Notifications, Challenges and Migration are
 * all this screen with different columns.
 *
 * Two things it will not grow, both already argued below where they would have gone: per-tab counts
 * and a stat row. Both need an aggregate `GET /api/invoices` does not return, and the alternatives —
 * seven extra requests, or summing the visible page — are worse than the absence.
 */
export default function InvoicesListPage() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<InvoiceStatus | 'all'>('all')

  const invoicesQuery = useInvoicesList({
    status: status === 'all' ? undefined : status,
    page,
    pageSize: 50,
  })
  const data = invoicesQuery.data

  /*
   * The tabs carry no counts, and that is a data limit rather than a style choice. A count per tab
   * needs either a per-status aggregate (GET /api/invoices returns none) or one extra request per
   * status — seven of them, fired on every page load, to decorate a filter. The pagination line
   * underneath already reports the count for whichever tab is open, which is the number staff are
   * actually reading.
   */
  const tabs: FilterTab<InvoiceStatus | 'all'>[] = [
    { key: 'all', label: 'All' },
    ...INVOICE_STATUSES.map((s) => ({
      key: s,
      label: statusLabel(s),
    })),
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Billing"
        description={data ? `${data.totalCount.toLocaleString()} invoices` : undefined}
        actions={<CreateInvoiceDialog />}
      />

      {/*
        No stat row above this list, though the mockup's language invites one. The honest figures a
        billing screen wants — total outstanding, collected this month — need an aggregate that
        GET /api/invoices does not return, and summing the current page would produce a number that
        silently changes meaning when you turn the page.
      */}

      <FilterTabs
        tabs={tabs}
        active={status}
        onChange={(key) => {
          setStatus(key)
          setPage(1)
        }}
      />

      {invoicesQuery.isError && (
        <ListError
          message="We couldn't load the invoice list"
          onRetry={() => invoicesQuery.refetch()}
          isRetrying={invoicesQuery.isFetching}
        />
      )}

      {invoicesQuery.isLoading && <ListSkeleton />}

      {!invoicesQuery.isLoading && data?.items.length === 0 && (
        <ListEmpty
          message="No invoices here."
          hint={status === 'all' ? undefined : `Nothing is currently ${statusLabel(status).toLowerCase()}.`}
        />
      )}

      {!invoicesQuery.isLoading && data && data.items.length > 0 && (
        <>
          {/* Mobile: card list — 7 columns of dates and currency have no room on a phone screen. */}
          <div className="space-y-2 md:hidden">
            {data.items.map((invoice) => (
              <button
                key={invoice.id}
                type="button"
                onClick={() => navigate(`/billing/${invoice.id}`)}
                className={cn(
                  'block w-full space-y-1.5 rounded-2xl border border-border p-3 text-left active:bg-accent',
                  invoice.status === 'Overdue' ? OVERDUE_ROW : 'bg-card'
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate font-medium tabular-nums">{invoice.invoiceNumber}</p>
                  <Badge variant={STATUS_VARIANT[invoice.status]} className="shrink-0">
                    {statusLabel(invoice.status)}
                  </Badge>
                </div>
                <p className="truncate text-sm text-muted-foreground">{invoice.memberName}</p>
                <div className="flex items-center justify-between gap-2 text-sm">
                  <span
                    className={cn(
                      'tabular-nums',
                      invoice.status === 'Overdue' ? 'font-medium text-destructive' : 'text-muted-foreground'
                    )}
                  >
                    Due {new Date(invoice.dueDate).toLocaleDateString()}
                  </span>
                  <span
                    className={cn(
                      'tabular-nums',
                      invoice.amountOutstanding > 0 ? 'font-medium text-warning' : 'text-muted-foreground',
                    )}
                  >
                    {invoice.amountOutstanding > 0
                      ? `${currency(invoice.amountOutstanding, invoice.currency)} due`
                      : currency(invoice.totalAmount, invoice.currency)}
                  </span>
                </div>
              </button>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden overflow-hidden rounded-panel border border-border bg-card edge-light-soft md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className={COLUMN_HEAD}>Invoice #</TableHead>
                  <TableHead className={COLUMN_HEAD}>Member</TableHead>
                  <TableHead className={COLUMN_HEAD}>Issued</TableHead>
                  <TableHead className={COLUMN_HEAD}>Due</TableHead>
                  <TableHead className={COLUMN_HEAD}>Total</TableHead>
                  <TableHead className={COLUMN_HEAD}>Outstanding</TableHead>
                  <TableHead className={COLUMN_HEAD}>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((invoice) => {
                  const isOverdue = invoice.status === 'Overdue'

                  return (
                    <TableRow
                      key={invoice.id}
                      className={cn('cursor-pointer', isOverdue && OVERDUE_ROW)}
                      onClick={() => navigate(`/billing/${invoice.id}`)}
                    >
                      <TableCell className="font-medium tabular-nums">{invoice.invoiceNumber}</TableCell>
                      <TableCell>{invoice.memberName}</TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {new Date(invoice.issueDate).toLocaleDateString()}
                      </TableCell>
                      {/* A due date that has passed is the reason the row is red at all, so it says
                          so itself rather than leaving the rail to carry the whole message. */}
                      <TableCell
                        className={cn(
                          'tabular-nums',
                          isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground',
                        )}
                      >
                        {new Date(invoice.dueDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell className="tabular-nums">{currency(invoice.totalAmount, invoice.currency)}</TableCell>
                      <TableCell
                        className={cn(
                          'tabular-nums',
                          invoice.amountOutstanding > 0 ? 'font-medium text-warning' : 'text-muted-foreground',
                        )}
                      >
                        {currency(invoice.amountOutstanding, invoice.currency)}
                      </TableCell>
                      <TableCell>
                        <Badge variant={STATUS_VARIANT[invoice.status]}>{statusLabel(invoice.status)}</Badge>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>

          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            totalCount={data.totalCount}
            hasPreviousPage={data.hasPreviousPage}
            hasNextPage={data.hasNextPage}
            onPageChange={setPage}
            itemLabel="invoices"
          />
        </>
      )}
    </div>
  )
}

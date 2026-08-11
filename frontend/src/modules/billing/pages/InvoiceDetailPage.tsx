import { useParams, Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { ListError, PageHeader } from '@/shared/components/console'
import { useInvoice } from '@/modules/billing/api/billingApi'
import { IssueRefundDialog } from '@/modules/billing/components/IssueRefundDialog'
import { RecordPaymentDialog } from '@/modules/billing/components/RecordPaymentDialog'
import { isStale } from '@/shared/lib/queryTrust'
import { useAuthStore } from '@/stores/authStore'

const currency = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

/**
 * The panel vocabulary every detail route in the console inherits: 20px radius, hairline border,
 * the light surface's soft cast shadow. Lead, Work order, Trainer, Import job and the full Member
 * route are all this frame with different contents.
 */
const PANEL = 'overflow-hidden rounded-panel border border-border bg-card edge-light-soft'
const PANEL_HEADING = 'font-display text-xl font-bold tracking-tight'
/** Uppercase eyebrow column headers, matching the list. */
const COLUMN_HEAD = 'text-[10px] font-bold tracking-[0.13em] text-muted-foreground uppercase'

/**
 * The detail half of the console's list/detail template.
 *
 * The header stacks member → number → status → dates, so the page answers "whose, which, what state"
 * before any table is read, and the totals stack ends in the one line somebody opened the page for.
 */
export default function InvoiceDetailPage() {
  // Reading an invoice and acting on one are different grants. Each control below is gated on the
  // permission its own endpoint enforces, so this screen never offers a button that can only 403.
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const canRecordPayment = hasPermission('billing.record_payment')
  const canIssueRefund = hasPermission('billing.issue_refund')

  const { id } = useParams<{ id: string }>()
  const invoiceQuery = useInvoice(id)
  const invoice = invoiceQuery.data

  const backLink = (
    <Link to="/billing" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
      <ArrowLeft className="size-4" />
      Back to invoices
    </Link>
  )

  /*
   * Loading and failure used to share one branch (`isLoading || !invoice`), which meant a deleted
   * invoice, a 403, or a dropped connection all left the skeleton pulsing forever with no way out —
   * the same trap the dashboard hit on a stale bookmark. They are separate states now, and the
   * failure one offers the retry.
   */
  if (isStale(invoiceQuery)) {
    return (
      <div className="space-y-6">
        {backLink}
        <ListError
          message="We couldn't load this invoice"
          onRetry={() => invoiceQuery.refetch()}
          isRetrying={invoiceQuery.isFetching}
        />
      </div>
    )
  }

  if (!invoice) {
    return (
      <div className="space-y-4">
        {backLink}
        <Skeleton className="h-10 w-48 rounded-2xl" />
        <Skeleton className="h-64 w-full rounded-2xl" />
      </div>
    )
  }

  const isSettled = invoice.amountOutstanding <= 0

  return (
    <div className="space-y-6">
      {backLink}

      <PageHeader
        eyebrow={invoice.memberName}
        title={
          <span className="flex flex-wrap items-center gap-3">
            <span className="tabular-nums">{invoice.invoiceNumber}</span>
            <Badge className="align-middle">{invoice.status}</Badge>
          </span>
        }
        description={
          <span className="tabular-nums">
            Issued {new Date(invoice.issueDate).toLocaleDateString()} · Due{' '}
            {new Date(invoice.dueDate).toLocaleDateString()}
          </span>
        }
        actions={
          canRecordPayment &&
          invoice.amountOutstanding > 0 && (
            <RecordPaymentDialog invoiceId={invoice.id} amountOutstanding={invoice.amountOutstanding} />
          )
        }
      />

      <div className={PANEL}>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className={COLUMN_HEAD}>Description</TableHead>
              <TableHead className={COLUMN_HEAD}>Qty</TableHead>
              <TableHead className={COLUMN_HEAD}>Unit price</TableHead>
              <TableHead className={COLUMN_HEAD}>Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {invoice.lines.map((line) => (
              <TableRow key={line.id}>
                <TableCell>{line.description}</TableCell>
                <TableCell className="tabular-nums">{line.quantity}</TableCell>
                <TableCell className="tabular-nums">{currency(line.unitPrice, invoice.currency)}</TableCell>
                <TableCell className="tabular-nums">{currency(line.lineTotal, invoice.currency)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        {/* The totals sit in a recessed well at the foot of the lines they total, rather than
            floating on the same white as the table. */}
        <div className="flex justify-end border-t border-border bg-[#FAFAF7] p-5">
          <div className="w-full max-w-xs space-y-1.5 text-sm tabular-nums">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Subtotal</span>
              <span>{currency(invoice.subtotal, invoice.currency)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Tax</span>
              <span>{currency(invoice.taxAmount, invoice.currency)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Discount</span>
              <span>-{currency(invoice.discountAmount, invoice.currency)}</span>
            </div>
            <div className="flex justify-between border-t border-border pt-1.5 font-semibold">
              <span>Total</span>
              <span>{currency(invoice.totalAmount, invoice.currency)}</span>
            </div>
            <div className="flex justify-between text-success">
              <span>Paid</span>
              <span>{currency(invoice.amountPaid, invoice.currency)}</span>
            </div>
            {/*
              The one line on this page somebody is chasing. It gets its own tinted well, the amber
              rail and a 22px display numeral — but only while there is something to chase. A settled
              invoice shows the same figure muted and inline with the rows above it, because an amber
              zero under a rail reads as a problem, and closing an invoice is the opposite of one.
            */}
            {isSettled ? (
              <div className="flex justify-between font-semibold text-muted-foreground">
                <span>Outstanding</span>
                <span>{currency(invoice.amountOutstanding, invoice.currency)}</span>
              </div>
            ) : (
              <div className="mt-2.5 flex items-center justify-between gap-4 rounded-[14px] bg-[#FDF6EC] px-3.5 py-3 rail-warning">
                <span className="font-bold text-[#B45309]">Outstanding</span>
                <span className="font-display text-[22px] leading-none font-black tracking-[-0.03em] text-[#B45309]">
                  {currency(invoice.amountOutstanding, invoice.currency)}
                </span>
              </div>
            )}
          </div>
        </div>
      </div>

      {invoice.payments.length > 0 && (
        <section className={PANEL}>
          <h2 className={cn(PANEL_HEADING, 'p-5 pb-0')}>Payments</h2>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className={COLUMN_HEAD}>Method</TableHead>
                <TableHead className={COLUMN_HEAD}>Amount</TableHead>
                <TableHead className={COLUMN_HEAD}>Paid at</TableHead>
                <TableHead className={COLUMN_HEAD}>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {invoice.payments.map((p) => (
                <TableRow key={p.id}>
                  <TableCell>{p.method}</TableCell>
                  <TableCell className="tabular-nums">{currency(p.amount, invoice.currency)}</TableCell>
                  <TableCell className="text-muted-foreground tabular-nums">
                    {new Date(p.paidAt).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{p.status}</Badge>
                  </TableCell>
                  <TableCell>
                    {/* A Receptionist holds billing.view, create_invoice and record_payment but
                        NOT issue_refund — reaching this screen is normal for them, refunding is not.
                        Absent rather than present-and-403, which is the difference between a role
                        boundary and a broken button. */}
                    {canIssueRefund && p.status === 'Completed' && (
                      <IssueRefundDialog invoiceId={invoice.id} paymentId={p.id} maxAmount={p.amount} />
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </section>
      )}

      {invoice.refunds.length > 0 && (
        <section className={PANEL}>
          <h2 className={cn(PANEL_HEADING, 'p-5 pb-0')}>Refunds</h2>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className={COLUMN_HEAD}>Amount</TableHead>
                <TableHead className={COLUMN_HEAD}>Reason</TableHead>
                <TableHead className={COLUMN_HEAD}>Refunded at</TableHead>
                <TableHead className={COLUMN_HEAD}>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {invoice.refunds.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="tabular-nums">{currency(r.amount, invoice.currency)}</TableCell>
                  <TableCell className="text-muted-foreground">{r.reason}</TableCell>
                  <TableCell className="text-muted-foreground tabular-nums">
                    {new Date(r.refundedAt).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{r.status}</Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </section>
      )}
    </div>
  )
}

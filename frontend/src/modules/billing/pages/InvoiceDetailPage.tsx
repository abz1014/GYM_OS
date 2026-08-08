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

const currency = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

const PANEL = 'overflow-hidden rounded-2xl border border-border bg-card shadow-sm'
const PANEL_HEADING = 'font-display text-xl font-bold tracking-tight'

export default function InvoiceDetailPage() {
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
  if (invoiceQuery.isError) {
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
          invoice.amountOutstanding > 0 && (
            <RecordPaymentDialog invoiceId={invoice.id} amountOutstanding={invoice.amountOutstanding} />
          )
        }
      />

      <div className={PANEL}>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Description</TableHead>
              <TableHead>Qty</TableHead>
              <TableHead>Unit price</TableHead>
              <TableHead>Total</TableHead>
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

        <div className="ml-auto w-full max-w-xs space-y-1.5 p-5 text-sm tabular-nums">
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
          {/* The one line on this page somebody is chasing. It carries the emphasis only while there
              is something to chase — a settled invoice showing an amber zero reads as a problem. */}
          <div
            className={cn(
              'flex justify-between font-semibold',
              isSettled ? 'text-muted-foreground' : 'text-warning',
            )}
          >
            <span>Outstanding</span>
            <span
              className={cn(!isSettled && 'font-display text-xl leading-none font-black tracking-tight')}
            >
              {currency(invoice.amountOutstanding, invoice.currency)}
            </span>
          </div>
        </div>
      </div>

      {invoice.payments.length > 0 && (
        <section className={PANEL}>
          <h2 className={cn(PANEL_HEADING, 'p-5 pb-0')}>Payments</h2>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Method</TableHead>
                <TableHead>Amount</TableHead>
                <TableHead>Paid at</TableHead>
                <TableHead>Status</TableHead>
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
                    {p.status === 'Completed' && (
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
                <TableHead>Amount</TableHead>
                <TableHead>Reason</TableHead>
                <TableHead>Refunded at</TableHead>
                <TableHead>Status</TableHead>
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

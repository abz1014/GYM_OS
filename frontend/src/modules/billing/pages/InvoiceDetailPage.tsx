import { useParams, Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useInvoice } from '@/modules/billing/api/billingApi'
import { IssueRefundDialog } from '@/modules/billing/components/IssueRefundDialog'
import { RecordPaymentDialog } from '@/modules/billing/components/RecordPaymentDialog'

const currency = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

export default function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: invoice, isLoading } = useInvoice(id)

  if (isLoading || !invoice) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link to="/billing" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to invoices
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{invoice.invoiceNumber}</h1>
            <Badge>{invoice.status}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {invoice.memberName} · Issued {new Date(invoice.issueDate).toLocaleDateString()} · Due{' '}
            {new Date(invoice.dueDate).toLocaleDateString()}
          </p>
        </div>
        {invoice.amountOutstanding > 0 && (
          <RecordPaymentDialog invoiceId={invoice.id} amountOutstanding={invoice.amountOutstanding} />
        )}
      </div>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Description</TableHead>
                <TableHead>Qty</TableHead>
                <TableHead>Unit Price</TableHead>
                <TableHead>Total</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {invoice.lines.map((line) => (
                <TableRow key={line.id}>
                  <TableCell>{line.description}</TableCell>
                  <TableCell>{line.quantity}</TableCell>
                  <TableCell>{currency(line.unitPrice, invoice.currency)}</TableCell>
                  <TableCell>{currency(line.lineTotal, invoice.currency)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <div className="mt-4 ml-auto w-full max-w-xs space-y-1 text-sm">
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
            <div className="flex justify-between font-semibold">
              <span>Total</span>
              <span>{currency(invoice.totalAmount, invoice.currency)}</span>
            </div>
            <div className="flex justify-between text-success">
              <span>Paid</span>
              <span>{currency(invoice.amountPaid, invoice.currency)}</span>
            </div>
            <div className="flex justify-between font-semibold">
              <span>Outstanding</span>
              <span>{currency(invoice.amountOutstanding, invoice.currency)}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {invoice.payments.length > 0 && (
        <Card>
          <CardContent>
            <h2 className="mb-3 font-medium">Payments</h2>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Method</TableHead>
                  <TableHead>Amount</TableHead>
                  <TableHead>Paid At</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {invoice.payments.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{p.method}</TableCell>
                    <TableCell>{currency(p.amount, invoice.currency)}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(p.paidAt).toLocaleString()}</TableCell>
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
          </CardContent>
        </Card>
      )}

      {invoice.refunds.length > 0 && (
        <Card>
          <CardContent>
            <h2 className="mb-3 font-medium">Refunds</h2>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Amount</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead>Refunded At</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {invoice.refunds.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{currency(r.amount, invoice.currency)}</TableCell>
                    <TableCell className="text-muted-foreground">{r.reason}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(r.refundedAt).toLocaleString()}</TableCell>
                    <TableCell>
                      <Badge variant="outline">{r.status}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

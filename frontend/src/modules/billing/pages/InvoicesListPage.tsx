import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
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

const currency = (amount: number, code: string) => amount.toLocaleString('en-US', { style: 'currency', currency: code })

export default function InvoicesListPage() {
  const navigate = useNavigate()
  const { data, isLoading } = useInvoicesList({ page: 1, pageSize: 50 })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Billing & Invoicing</h1>
          <p className="text-sm text-muted-foreground">{data?.totalCount ?? '—'} invoices</p>
        </div>
        <CreateInvoiceDialog />
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full md:h-10" />
          ))}
        </div>
      )}

      {!isLoading && data?.items.length === 0 && (
        <p className="py-8 text-center text-sm text-muted-foreground">No invoices yet.</p>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <>
          {/* Mobile: card list — 7 columns of dates/currency have no room on a phone screen. */}
          <div className="space-y-2 md:hidden">
            {data.items.map((invoice) => (
              <button
                key={invoice.id}
                type="button"
                onClick={() => navigate(`/billing/${invoice.id}`)}
                className="block w-full space-y-1.5 rounded-lg border bg-card p-3 text-left active:bg-accent"
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate font-medium">{invoice.invoiceNumber}</p>
                  <Badge variant={STATUS_VARIANT[invoice.status]} className="shrink-0">
                    {invoice.status}
                  </Badge>
                </div>
                <p className="truncate text-sm text-muted-foreground">{invoice.memberName}</p>
                <div className="flex items-center justify-between gap-2 text-sm">
                  <span className="text-muted-foreground">Due {new Date(invoice.dueDate).toLocaleDateString()}</span>
                  <span className={invoice.amountOutstanding > 0 ? 'font-medium text-warning' : 'text-muted-foreground'}>
                    {invoice.amountOutstanding > 0
                      ? `${currency(invoice.amountOutstanding, invoice.currency)} due`
                      : currency(invoice.totalAmount, invoice.currency)}
                  </span>
                </div>
              </button>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Invoice #</TableHead>
                  <TableHead>Member</TableHead>
                  <TableHead>Issued</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Outstanding</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((invoice) => (
                  <TableRow key={invoice.id} className="cursor-pointer" onClick={() => navigate(`/billing/${invoice.id}`)}>
                    <TableCell className="font-medium">{invoice.invoiceNumber}</TableCell>
                    <TableCell>{invoice.memberName}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(invoice.issueDate).toLocaleDateString()}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(invoice.dueDate).toLocaleDateString()}</TableCell>
                    <TableCell>{currency(invoice.totalAmount, invoice.currency)}</TableCell>
                    <TableCell className={invoice.amountOutstanding > 0 ? 'text-warning font-medium' : 'text-muted-foreground'}>
                      {currency(invoice.amountOutstanding, invoice.currency)}
                    </TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANT[invoice.status]}>{invoice.status}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  )
}

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

      <div className="rounded-lg border">
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
            {isLoading &&
              Array.from({ length: 8 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={7}>
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {!isLoading && data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                  No invoices yet.
                </TableCell>
              </TableRow>
            )}

            {data?.items.map((invoice) => (
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
    </div>
  )
}

import { useState } from 'react'
import { Loader2, Receipt } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { useSuppliers } from '@/modules/equipment/api/equipmentApi'
import { useInventoryItem, useRecordPurchase } from '@/modules/inventory/api/inventoryApi'

const dateFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

const SECTION_LABEL_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'
const HEAD_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

export function InventoryItemDetailDialog({ itemId, itemName }: { itemId: string; itemName: string }) {
  const [open, setOpen] = useState(false)
  const [supplierId, setSupplierId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [unitCost, setUnitCost] = useState(0)
  const [invoiceReference, setInvoiceReference] = useState('')

  const { data: item, isLoading } = useInventoryItem(open ? itemId : undefined)
  const { data: suppliers } = useSuppliers()
  const recordPurchase = useRecordPurchase(itemId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    recordPurchase.mutate(
      { supplierId: supplierId || undefined, quantity, unitCost, invoiceReference: invoiceReference || undefined },
      {
        onSuccess: () => {
          toast.success('Purchase recorded.')
          setQuantity(1)
          setUnitCost(0)
          setInvoiceReference('')
        },
        onError: () => toast.error('Could not record purchase.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="icon" variant="outline" className="size-8 rounded-xl" title="Purchases & stock history">
          <Receipt className="size-3.5" />
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="font-display text-xl font-bold tracking-tight">
            {itemName} — purchases &amp; stock history
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-3 rounded-2xl border border-border p-4">
          <p className={SECTION_LABEL_CLASS}>Record purchase</p>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Supplier (optional)</Label>
              <Select value={supplierId} onValueChange={setSupplierId}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Select supplier" />
                </SelectTrigger>
                <SelectContent>
                  {suppliers?.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="invoiceReference">Invoice reference (optional)</Label>
              <Input
                id="invoiceReference"
                className="tabular-nums"
                value={invoiceReference}
                onChange={(e) => setInvoiceReference(e.target.value)}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="quantity">Quantity</Label>
              <Input
                id="quantity"
                type="number"
                min={1}
                required
                className="tabular-nums"
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              {/* No currency beside the cost fields anywhere in this dialog: /api/inventory returns
                  unitCost and unitPrice as bare decimals and never says what they are denominated
                  in, so a symbol here would be this screen's own invention. */}
              <Label htmlFor="unitCost">Unit cost</Label>
              <Input
                id="unitCost"
                type="number"
                min={0}
                step="0.01"
                required
                className="tabular-nums"
                value={unitCost}
                onChange={(e) => setUnitCost(Number(e.target.value))}
              />
            </div>
          </div>
          <div className="flex justify-end">
            <Button type="submit" className="rounded-xl" disabled={recordPurchase.isPending}>
              {recordPurchase.isPending && <Loader2 className="size-4 animate-spin" />}
              Record purchase
            </Button>
          </div>
        </form>

        <div className="space-y-2">
          <p className={SECTION_LABEL_CLASS}>Purchase history</p>
          <div className="max-h-48 overflow-y-auto rounded-2xl border border-border">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className={HEAD_CLASS}>Date</TableHead>
                  <TableHead className={HEAD_CLASS}>Supplier</TableHead>
                  <TableHead className={HEAD_CLASS}>Qty</TableHead>
                  <TableHead className={HEAD_CLASS}>Unit cost</TableHead>
                  <TableHead className={HEAD_CLASS}>Invoice</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading && (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      Loading…
                    </TableCell>
                  </TableRow>
                )}
                {item?.purchaseRecords.length === 0 && !isLoading && (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      No purchases recorded yet.
                    </TableCell>
                  </TableRow>
                )}
                {item?.purchaseRecords.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="tabular-nums">{dateFormat.format(new Date(p.purchasedAt))}</TableCell>
                    <TableCell className="text-muted-foreground">{p.supplierName ?? '—'}</TableCell>
                    <TableCell className="tabular-nums">{p.quantity.toLocaleString()}</TableCell>
                    <TableCell className="tabular-nums">{p.unitCost.toFixed(2)}</TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">{p.invoiceReference ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>

        <div className="space-y-2">
          {/* This table is routinely empty and that is the honest answer: nothing in this system
              seeds stock movements, and the two adjust buttons on the list are the only thing that
              writes one. It stays visible rather than hidden so a manager can see that the ledger
              exists and is genuinely blank, instead of wondering where the history went. */}
          <p className={SECTION_LABEL_CLASS}>Recent stock movements</p>
          <div className="max-h-48 overflow-y-auto rounded-2xl border border-border">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className={HEAD_CLASS}>Date</TableHead>
                  <TableHead className={HEAD_CLASS}>Type</TableHead>
                  <TableHead className={HEAD_CLASS}>Qty</TableHead>
                  <TableHead className={HEAD_CLASS}>Reason</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {item?.stockMovements.length === 0 && !isLoading && (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                      No stock movements yet.
                    </TableCell>
                  </TableRow>
                )}
                {item?.stockMovements.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell className="tabular-nums">{dateFormat.format(new Date(m.movedAt))}</TableCell>
                    <TableCell>
                      <span
                        className={cn(
                          'inline-flex items-center rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
                          m.type === 'In' ? 'bg-success/10 text-success' : 'bg-muted text-muted-foreground',
                        )}
                      >
                        {m.type === 'In' ? 'In' : 'Out'}
                      </span>
                    </TableCell>
                    <TableCell className="tabular-nums">{m.quantity.toLocaleString()}</TableCell>
                    <TableCell className="text-muted-foreground">{m.reason ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

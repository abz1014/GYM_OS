import { useState } from 'react'
import { Loader2, Receipt } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useSuppliers } from '@/modules/equipment/api/equipmentApi'
import { useInventoryItem, useRecordPurchase } from '@/modules/inventory/api/inventoryApi'

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
        <Button size="icon" variant="outline" className="size-7" title="Purchases & stock history">
          <Receipt className="size-3.5" />
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{itemName} — purchases &amp; stock history</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-3 rounded-lg border p-3">
          <p className="text-sm font-medium">Record purchase</p>
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
              <Input id="invoiceReference" value={invoiceReference} onChange={(e) => setInvoiceReference(e.target.value)} />
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
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="unitCost">Unit cost</Label>
              <Input
                id="unitCost"
                type="number"
                min={0}
                step="0.01"
                required
                value={unitCost}
                onChange={(e) => setUnitCost(Number(e.target.value))}
              />
            </div>
          </div>
          <div className="flex justify-end">
            <Button type="submit" size="sm" disabled={recordPurchase.isPending}>
              {recordPurchase.isPending && <Loader2 className="size-4 animate-spin" />}
              Record Purchase
            </Button>
          </div>
        </form>

        <div className="space-y-2">
          <p className="text-sm font-medium">Purchase history</p>
          <div className="max-h-48 overflow-y-auto rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Qty</TableHead>
                  <TableHead>Unit Cost</TableHead>
                  <TableHead>Invoice</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      Loading…
                    </TableCell>
                  </TableRow>
                )}
                {item?.purchaseRecords.length === 0 && !isLoading && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      No purchases recorded yet.
                    </TableCell>
                  </TableRow>
                )}
                {item?.purchaseRecords.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{new Date(p.purchasedAt).toLocaleDateString()}</TableCell>
                    <TableCell className="text-muted-foreground">{p.supplierName ?? '—'}</TableCell>
                    <TableCell>{p.quantity}</TableCell>
                    <TableCell>{p.unitCost.toFixed(2)}</TableCell>
                    <TableCell className="text-muted-foreground">{p.invoiceReference ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>

        <div className="space-y-2">
          <p className="text-sm font-medium">Recent stock movements</p>
          <div className="max-h-48 overflow-y-auto rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Qty</TableHead>
                  <TableHead>Reason</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {item?.stockMovements.length === 0 && !isLoading && (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                      No stock movements yet.
                    </TableCell>
                  </TableRow>
                )}
                {item?.stockMovements.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell>{new Date(m.movedAt).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <Badge variant={m.type === 'In' ? 'default' : 'secondary'}>{m.type}</Badge>
                    </TableCell>
                    <TableCell>{m.quantity}</TableCell>
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

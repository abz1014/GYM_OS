import { useState } from 'react'
import { Loader2, Plus } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useCreateInventoryItem, type InventoryCategory } from '@/modules/inventory/api/inventoryApi'
import { useUiStore } from '@/stores/uiStore'

const CATEGORIES: InventoryCategory[] = ['Supplement', 'Merchandise', 'CleaningSupply', 'SparePart']

export function CreateInventoryItemDialog() {
  const [open, setOpen] = useState(false)
  const [sku, setSku] = useState('')
  const [name, setName] = useState('')
  const [category, setCategory] = useState<InventoryCategory>('Supplement')
  const [quantityOnHand, setQuantityOnHand] = useState(0)
  const [reorderLevel, setReorderLevel] = useState(10)

  const branchId = useUiStore((s) => s.selectedBranchId)
  const createItem = useCreateInventoryItem()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!branchId) {
      toast.error('Select a branch first.')
      return
    }

    createItem.mutate(
      { sku, name, category, branchId, quantityOnHand, reorderLevel },
      {
        onSuccess: () => {
          toast.success('Item added.')
          setOpen(false)
          setSku('')
          setName('')
        },
        onError: () => toast.error('Could not add item — check the SKU is unique.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          Add Item
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add inventory item</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="sku">SKU</Label>
              <Input id="sku" required value={sku} onChange={(e) => setSku(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Category</Label>
              <Select value={category} onValueChange={(v) => setCategory(v as InventoryCategory)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {c}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="quantityOnHand">Initial quantity</Label>
              <Input
                id="quantityOnHand"
                type="number"
                min={0}
                value={quantityOnHand}
                onChange={(e) => setQuantityOnHand(Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="reorderLevel">Reorder level</Label>
              <Input
                id="reorderLevel"
                type="number"
                min={0}
                value={reorderLevel}
                onChange={(e) => setReorderLevel(Number(e.target.value))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createItem.isPending}>
              {createItem.isPending && <Loader2 className="size-4 animate-spin" />}
              Add Item
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

import { Minus, Plus } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { useRecordStockMovement } from '@/modules/inventory/api/inventoryApi'

export function StockAdjustButtons({ itemId }: { itemId: string }) {
  const recordMovement = useRecordStockMovement(itemId)

  const adjust = (type: 'In' | 'Out') => {
    recordMovement.mutate(
      { type, quantity: 1, reason: type === 'In' ? 'Manual restock' : 'Manual deduction' },
      { onError: (err: unknown) => toast.error((err as { response?: { data?: { title?: string } } })?.response?.data?.title ?? 'Could not update stock.') }
    )
  }

  return (
    <div className="flex items-center gap-1">
      <Button size="icon" variant="outline" className="size-7" disabled={recordMovement.isPending} onClick={() => adjust('Out')}>
        <Minus className="size-3.5" />
      </Button>
      <Button size="icon" variant="outline" className="size-7" disabled={recordMovement.isPending} onClick={() => adjust('In')}>
        <Plus className="size-3.5" />
      </Button>
    </div>
  )
}

import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useInventoryItemsList } from '@/modules/inventory/api/inventoryApi'
import { CreateInventoryItemDialog } from '@/modules/inventory/components/CreateInventoryItemDialog'
import { InventoryItemDetailDialog } from '@/modules/inventory/components/InventoryItemDetailDialog'
import { StockAdjustButtons } from '@/modules/inventory/components/StockAdjustButtons'
import { useUiStore } from '@/stores/uiStore'

export default function InventoryPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const { data: items, isLoading } = useInventoryItemsList({ branchId, lowStockOnly: lowStockOnly || undefined })

  const lowStockCount = items?.filter((i) => i.isLowStock).length ?? 0

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Inventory</h1>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            {items?.length ?? '—'} items
            {lowStockCount > 0 && (
              <span className="flex items-center gap-1 text-destructive">
                <AlertTriangle className="size-3.5" /> {lowStockCount} low stock
              </span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant={lowStockOnly ? 'default' : 'outline'} size="sm" onClick={() => setLowStockOnly((v) => !v)}>
            Low stock only
          </Button>
          <CreateInventoryItemDialog />
        </div>
      </div>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Item</TableHead>
              <TableHead>SKU</TableHead>
              <TableHead>Category</TableHead>
              <TableHead>On Hand</TableHead>
              <TableHead>Reorder Level</TableHead>
              <TableHead>Adjust</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading &&
              Array.from({ length: 8 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={6}>
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {items?.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="font-medium">{item.name}</TableCell>
                <TableCell className="text-muted-foreground">{item.sku}</TableCell>
                <TableCell className="text-muted-foreground">{item.category}</TableCell>
                <TableCell className={item.isLowStock ? 'font-medium text-destructive' : ''}>
                  {item.quantityOnHand}
                  {item.isLowStock && (
                    <Badge variant="destructive" className="ml-2 text-[10px]">
                      Low
                    </Badge>
                  )}
                </TableCell>
                <TableCell className="text-muted-foreground">{item.reorderLevel}</TableCell>
                <TableCell>
                  <div className="flex items-center gap-1">
                    <StockAdjustButtons itemId={item.id} />
                    <InventoryItemDetailDialog itemId={item.id} itemName={item.name} />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

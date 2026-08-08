import { useState } from 'react'

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { Pagination } from '@/shared/components/Pagination'
import { FilterTabs, ListEmpty, ListError, ListSkeleton, PageHeader, SEVERITY_ROW, type FilterTab } from '@/shared/components/console'
import {
  inventoryCategoryLabel,
  useInventoryItemsList,
  type InventoryItemListItem,
} from '@/modules/inventory/api/inventoryApi'
import { CreateInventoryItemDialog } from '@/modules/inventory/components/CreateInventoryItemDialog'
import { InventoryItemDetailDialog } from '@/modules/inventory/components/InventoryItemDetailDialog'
import { StockAdjustButtons } from '@/modules/inventory/components/StockAdjustButtons'
import { useUiStore } from '@/stores/uiStore'

const HEAD_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

/**
 * Nothing at all on a healthy row. "Low" and "Out of stock" are two different problems — one is a
 * reorder, the other is a member being told no at the counter — and a green "In stock" chip on the
 * other ninety rows is what stops anyone noticing either.
 */
function StockPill({ item }: { item: InventoryItemListItem }) {
  if (item.quantityOnHand === 0) {
    return (
      <span className="inline-flex items-center rounded-xl bg-destructive/10 px-2.5 py-1 text-xs font-medium whitespace-nowrap text-destructive">
        Out of stock
      </span>
    )
  }
  if (item.isLowStock) {
    return (
      <span className="inline-flex items-center rounded-xl bg-warning/10 px-2.5 py-1 text-xs font-medium whitespace-nowrap text-warning">
        Low
      </span>
    )
  }
  return null
}

const quantityTone = (item: InventoryItemListItem) =>
  item.quantityOnHand === 0 ? 'font-medium text-destructive' : item.isLowStock ? 'font-medium text-warning' : ''

/**
 * The row's severity ground, on the same two-tier split the pill already makes: out of stock is a
 * member being turned away at the counter, low is a reorder. A healthy row gets nothing, which is
 * what leaves the other two visible.
 */
const stockRowTone = (item: InventoryItemListItem) =>
  item.quantityOnHand === 0 ? SEVERITY_ROW.destructive : item.isLowStock ? SEVERITY_ROW.warning : null

export default function InventoryPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [page, setPage] = useState(1)
  const itemsQuery = useInventoryItemsList({
    branchId,
    lowStockOnly: lowStockOnly || undefined,
    page,
    pageSize: 20,
  })
  const data = itemsQuery.data
  const items = data?.items

  // Computed from a dedicated pageSize:1 query rather than the visible page's items — with real
  // pagination the current page rarely holds every low-stock item, so counting only what's
  // rendered would undercount as soon as there's more than one page.
  const { data: lowStockSummary } = useInventoryItemsList({ branchId, lowStockOnly: true, page: 1, pageSize: 1 })

  /*
   * The Low stock tab carries a real count; All deliberately carries none. There is one totalCount
   * in this response and it belongs to whatever filter was sent, so on the Low stock tab it *is*
   * the low-stock count — using it for All would print the smaller number under the wider word.
   * The pagination line underneath already reports the count for whichever tab is open.
   *
   * The count is undefined until its query answers rather than falling back to 0, because a "0"
   * that is about to become a 14 is the one number staff would act on.
   */
  const tabs: FilterTab<'all' | 'low'>[] = [
    { key: 'all', label: 'All items' },
    {
      key: 'low',
      label: 'Low stock',
      count: lowStockSummary?.totalCount,
      countClassName: 'text-warning',
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Inventory"
        description={data ? `${data.totalCount.toLocaleString()} items` : undefined}
        actions={<CreateInventoryItemDialog />}
      />

      {/*
        No stat row above this list. The figures a stockroom actually wants — value on hand, spend
        this month — need an aggregate GET /api/inventory does not return: it answers with one page
        of items, each carrying a bare unitPrice, and multiplying the twenty rows on screen would
        produce a "stock value" that changed every time someone paged. Nor is there a currency
        anywhere in this response to print such a total in.
      */}

      <FilterTabs
        tabs={tabs}
        active={lowStockOnly ? 'low' : 'all'}
        onChange={(key) => {
          setLowStockOnly(key === 'low')
          setPage(1)
        }}
      />

      {itemsQuery.isError && (
        <ListError
          message="We couldn't load the inventory list"
          onRetry={() => itemsQuery.refetch()}
          isRetrying={itemsQuery.isFetching}
        />
      )}

      {itemsQuery.isLoading && <ListSkeleton />}

      {!itemsQuery.isLoading && items?.length === 0 && (
        <ListEmpty
          message="No items here."
          hint={lowStockOnly ? 'Nothing is at or below its reorder level.' : undefined}
        />
      )}

      {!itemsQuery.isLoading && data && items && items.length > 0 && (
        <>
          {/* Mobile: card list. Not a tap target — the row's actions are the two adjust buttons and
              the history dialog inside it, so the card stays a plain container. */}
          <div className="space-y-2 md:hidden">
            {items.map((item) => (
              <div
                key={item.id}
                className={cn(
                  'space-y-2 rounded-panel border border-border p-3',
                  stockRowTone(item) ?? 'bg-card edge-light-soft',
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate font-medium">{item.name}</p>
                  <StockPill item={item} />
                </div>
                <p className="text-sm text-muted-foreground">
                  <span className="tabular-nums">{item.sku}</span> · {inventoryCategoryLabel(item.category)}
                </p>
                <p className={cn('text-sm tabular-nums', quantityTone(item) || 'text-muted-foreground')}>
                  {item.quantityOnHand.toLocaleString()} on hand · reorder at {item.reorderLevel.toLocaleString()}
                </p>
                <div className="flex items-center gap-1">
                  <StockAdjustButtons itemId={item.id} />
                  <InventoryItemDetailDialog itemId={item.id} itemName={item.name} />
                </div>
              </div>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className={HEAD_CLASS}>Item</TableHead>
                  <TableHead className={HEAD_CLASS}>SKU</TableHead>
                  <TableHead className={HEAD_CLASS}>Category</TableHead>
                  <TableHead className={HEAD_CLASS}>On hand</TableHead>
                  <TableHead className={HEAD_CLASS}>Reorder level</TableHead>
                  {/*
                    No Price column, though InventoryItemListItem carries unitPrice. It is a bare
                    decimal: /api/inventory returns no currency code and no branch to look one up
                    from, so the column could only be a number with a symbol invented beside it.
                  */}
                  <TableHead className={HEAD_CLASS}>Adjust</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((item) => (
                  <TableRow key={item.id} className={cn(stockRowTone(item))}>
                    <TableCell className="font-medium">{item.name}</TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">{item.sku}</TableCell>
                    <TableCell className="text-muted-foreground">{inventoryCategoryLabel(item.category)}</TableCell>
                    <TableCell>
                      <span className="flex items-center gap-2">
                        <span className={cn('tabular-nums', quantityTone(item))}>
                          {item.quantityOnHand.toLocaleString()}
                        </span>
                        <StockPill item={item} />
                      </span>
                    </TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">
                      {item.reorderLevel.toLocaleString()}
                    </TableCell>
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

          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            totalCount={data.totalCount}
            hasPreviousPage={data.hasPreviousPage}
            hasNextPage={data.hasNextPage}
            onPageChange={setPage}
            itemLabel="items"
          />
        </>
      )}
    </div>
  )
}

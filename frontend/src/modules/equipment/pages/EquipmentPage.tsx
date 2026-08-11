import { useState } from 'react'
import { QrCode } from 'lucide-react'

import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { Pagination } from '@/shared/components/Pagination'
import { ListEmpty, ListError, ListSkeleton, PageHeader, SEVERITY_ROW } from '@/shared/components/console'
import { useAssetsList, useUpdateAssetStatus, type AssetStatus } from '@/modules/equipment/api/equipmentApi'
import { CreateAssetDialog } from '@/modules/equipment/components/CreateAssetDialog'
import { CreateSupplierDialog } from '@/modules/equipment/components/CreateSupplierDialog'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import { isStale } from '@/shared/lib/queryTrust'

const STATUSES: AssetStatus[] = ['Active', 'UnderMaintenance', 'OutOfService', 'Retired']

const dateFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

/** AssetStatus is a C# enum; "OutOfService" is a wire value, not something to show a person. */
const STATUS_LABEL: Record<AssetStatus, string> = {
  Active: 'Active',
  UnderMaintenance: 'Under maintenance',
  OutOfService: 'Out of service',
  Retired: 'Retired',
}

/**
 * Severity, not decoration: a machine that is out of service is costing the floor money right now,
 * one under maintenance is already someone's problem, and a retired asset is history. Retired stays
 * grey rather than red so it doesn't compete for attention with the one status a manager must act on.
 */
const STATUS_CHIP: Record<AssetStatus, string> = {
  Active: 'bg-success/10 text-success',
  UnderMaintenance: 'bg-warning/10 text-warning',
  OutOfService: 'bg-destructive/10 text-destructive',
  Retired: 'bg-muted text-muted-foreground',
}

const STATUS_DOT: Record<AssetStatus, string> = {
  Active: 'bg-success',
  UnderMaintenance: 'bg-warning',
  OutOfService: 'bg-destructive',
  Retired: 'bg-muted-foreground',
}

/**
 * The row ground, on the same reasoning as the chip above — and deliberately narrower. Only the two
 * statuses someone has to do something about get a rail; Active is the healthy majority, and Retired
 * is history, so a rail on either would be the decoration this list already refuses in its pills.
 */
const STATUS_ROW: Partial<Record<AssetStatus, string>> = {
  UnderMaintenance: SEVERITY_ROW.warning,
  OutOfService: SEVERITY_ROW.destructive,
}

function AssetStatusPill({ status }: { status: AssetStatus }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
        STATUS_CHIP[status],
      )}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', STATUS_DOT[status])} />
      {STATUS_LABEL[status]}
    </span>
  )
}

/**
 * The status of one asset — a dropdown for whoever can change it, the pill alone for everyone else.
 *
 * Maintenance holds equipment.view but not equipment.manage, so for them this cell used to be a live
 * control over PUT /api/equipment/{id}/status that 403'd on every use. It failed in the quietest way
 * this console can fail: the Select is controlled by server data, so the value snapped back and
 * nothing was said. The pill is what the row was always for — the status is still on screen, it just
 * isn't offered as something to change by someone who can't.
 */
function AssetStatusCell({ assetId, status }: { assetId: string; status: AssetStatus }) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const updateStatus = useUpdateAssetStatus(assetId)

  if (!hasPermission('equipment.manage')) {
    return <AssetStatusPill status={status} />
  }

  return (
    <Select value={status} onValueChange={(v) => updateStatus.mutate(v as AssetStatus)}>
      {/* No aria-label: the trigger's accessible name is its content, which is the current status —
          labelling it would name the control and lose the value a screen reader reads out. */}
      <SelectTrigger size="sm" className="w-[196px] rounded-xl">
        <AssetStatusPill status={status} />
      </SelectTrigger>
      <SelectContent>
        {STATUSES.map((s) => (
          <SelectItem key={s} value={s}>
            {STATUS_LABEL[s]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

export default function EquipmentPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const [page, setPage] = useState(1)
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const assetsQuery = useAssetsList({ branchId, page, pageSize: 25 })
  const data = assetsQuery.data
  const assets = data?.items

  // Both dialogs post to endpoints behind equipment.manage. Absent rather than disabled, so the
  // header simply carries no actions for a role that has none — the same thing the sidebar does
  // with a module somebody can't open.
  const canManage = hasPermission('equipment.manage')

  return (
    <div className="space-y-4">
      <PageHeader
        title="Equipment"
        description={data ? `${data.totalCount.toLocaleString()} assets` : undefined}
        actions={
          canManage ? (
            <>
              <CreateSupplierDialog />
              <CreateAssetDialog />
            </>
          ) : undefined
        }
      />

      {/*
        No stat row and no status tabs above this list, and both absences are the same data limit.
        GET /api/equipment answers with one page of assets plus a totalCount that already honours
        whatever filter was sent — it reports no per-status aggregate, so "3 out of service" could
        only come from four extra requests fired on every load, or from counting the 25 rows that
        happen to be on screen. The second is worse than no number at all: it would read as a gym
        total and quietly change every time someone turned the page. The status of every asset is
        on its own row, where it is true.

        There is no search box either. /api/equipment takes branchId, status, category and paging
        and no search term, so the box could only filter the current page — a search that finds
        nothing on page 2 is a bug staff would report as missing equipment.
      */}

      {isStale(assetsQuery) && (
        <ListError
          message="We couldn't load the equipment list"
          onRetry={() => assetsQuery.refetch()}
          isRetrying={assetsQuery.isFetching}
        />
      )}

      {assetsQuery.isLoading && <ListSkeleton />}

      {!assetsQuery.isLoading && assets?.length === 0 && (
        <ListEmpty message="No equipment here." hint="Assets added for this branch will show up in this list." />
      )}

      {!assetsQuery.isLoading && data && assets && assets.length > 0 && (
        <>
          {/* Mobile: card list — six columns of tags, suppliers and dates have no room on a phone. */}
          <div className="space-y-2 md:hidden">
            {assets.map((asset) => (
              <div
                key={asset.id}
                className={cn(
                  'space-y-2 rounded-panel border border-border p-3',
                  STATUS_ROW[asset.status] ?? 'bg-card edge-light-soft',
                )}
              >
                <div className="flex items-center gap-2 font-medium">
                  <QrCode className="size-4 shrink-0 text-muted-foreground" />
                  <span className="truncate">{asset.name}</span>
                </div>
                <p className="text-sm text-muted-foreground">
                  <span className="tabular-nums">{asset.assetTag}</span> · {asset.category ?? 'Uncategorized'}
                </p>
                <p className="text-sm text-muted-foreground">
                  {asset.supplierName ?? 'No supplier'}
                  {asset.warrantyExpiresAt && (
                    <>
                      {' · Warranty to '}
                      <span className="tabular-nums">{dateFormat.format(new Date(asset.warrantyExpiresAt))}</span>
                    </>
                  )}
                </p>
                <AssetStatusCell assetId={asset.id} status={asset.status} />
              </div>
            ))}
          </div>

          {/* Desktop / tablet: full table */}
          <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Asset
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Tag
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Category
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Supplier
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Warranty
                  </TableHead>
                  <TableHead className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Status
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {assets.map((asset) => (
                  <TableRow key={asset.id} className={cn(STATUS_ROW[asset.status])}>
                    <TableCell className="font-medium">
                      <span className="flex items-center gap-2.5">
                        <QrCode className="size-4 shrink-0 text-muted-foreground" />
                        {asset.name}
                      </span>
                    </TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">{asset.assetTag}</TableCell>
                    <TableCell className="text-muted-foreground">{asset.category ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground">{asset.supplierName ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">
                      {asset.warrantyExpiresAt ? dateFormat.format(new Date(asset.warrantyExpiresAt)) : '—'}
                    </TableCell>
                    <TableCell>
                      <AssetStatusCell assetId={asset.id} status={asset.status} />
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
            itemLabel="assets"
          />
        </>
      )}
    </div>
  )
}

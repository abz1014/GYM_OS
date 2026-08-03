import { QrCode } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAssetsList, useUpdateAssetStatus, type AssetStatus } from '@/modules/equipment/api/equipmentApi'
import { CreateAssetDialog } from '@/modules/equipment/components/CreateAssetDialog'
import { CreateSupplierDialog } from '@/modules/equipment/components/CreateSupplierDialog'
import { useUiStore } from '@/stores/uiStore'

const STATUSES: AssetStatus[] = ['Active', 'UnderMaintenance', 'OutOfService', 'Retired']

const STATUS_VARIANT: Record<AssetStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  UnderMaintenance: 'secondary',
  OutOfService: 'destructive',
  Retired: 'outline',
}

function AssetStatusCell({ assetId, status }: { assetId: string; status: AssetStatus }) {
  const updateStatus = useUpdateAssetStatus(assetId)

  return (
    <Select value={status} onValueChange={(v) => updateStatus.mutate(v as AssetStatus)}>
      <SelectTrigger size="sm" className="w-[160px]">
        <Badge variant={STATUS_VARIANT[status]} className="mr-1">
          {status}
        </Badge>
      </SelectTrigger>
      <SelectContent>
        {STATUSES.map((s) => (
          <SelectItem key={s} value={s}>
            {s}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

export default function EquipmentPage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data, isLoading } = useAssetsList({ branchId, page: 1, pageSize: 100 })
  const assets = data?.items

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Equipment</h1>
          <p className="text-sm text-muted-foreground">{data?.totalCount ?? '—'} assets</p>
        </div>
        <div className="flex items-center gap-2">
          <CreateSupplierDialog />
          <CreateAssetDialog />
        </div>
      </div>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Asset</TableHead>
              <TableHead>Tag</TableHead>
              <TableHead>Category</TableHead>
              <TableHead>Supplier</TableHead>
              <TableHead>Warranty</TableHead>
              <TableHead>Status</TableHead>
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

            {assets?.map((asset) => (
              <TableRow key={asset.id}>
                <TableCell className="flex items-center gap-2 font-medium">
                  <QrCode className="size-4 text-muted-foreground" />
                  {asset.name}
                </TableCell>
                <TableCell className="text-muted-foreground">{asset.assetTag}</TableCell>
                <TableCell className="text-muted-foreground">{asset.category ?? '—'}</TableCell>
                <TableCell className="text-muted-foreground">{asset.supplierName ?? '—'}</TableCell>
                <TableCell className="text-muted-foreground">
                  {asset.warrantyExpiresAt ? new Date(asset.warrantyExpiresAt).toLocaleDateString() : '—'}
                </TableCell>
                <TableCell>
                  <AssetStatusCell assetId={asset.id} status={asset.status} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

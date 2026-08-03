import { useState } from 'react'
import { Download } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAssetsList } from '@/modules/equipment/api/equipmentApi'
import { useInventoryItemsList } from '@/modules/inventory/api/inventoryApi'
import { useWorkOrdersList } from '@/modules/maintenance/api/maintenanceApi'
import {
  exportAttendanceReport,
  exportCrmPipelineReport,
  exportEquipmentDowntimeReport,
  exportInventoryStockMovementReport,
  exportMembershipReport,
  exportRevenueReport,
  exportTrainerCommissionReport,
  useAttendanceReport,
  useCrmPipelineConversionReport,
  useEquipmentDowntimeReport,
  useInventoryStockMovementReport,
  useMembershipBreakdownReport,
  useRevenueReport,
  useTrainerCommissionReport,
} from '@/modules/reports/api/reportsApi'
import { SimpleBarChart } from '@/modules/reports/components/SimpleBarChart'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'

function ExportButton({ onExport }: { onExport: () => Promise<void> }) {
  const [isExporting, setIsExporting] = useState(false)

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={isExporting}
      onClick={async () => {
        setIsExporting(true)
        try {
          await onExport()
        } catch {
          toast.error('Failed to export report')
        } finally {
          setIsExporting(false)
        }
      }}
    >
      <Download />
      {isExporting ? 'Exporting...' : 'Export to Excel'}
    </Button>
  )
}

function ReportCard({
  title,
  action,
  children,
}: {
  title: string
  action?: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <p className="font-medium">{title}</p>
        {action}
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

function RevenueTab() {
  const { data, isLoading } = useRevenueReport(6)

  return (
    <ReportCard title="Revenue (last 6 months)" action={<ExportButton onExport={() => exportRevenueReport(6)} />}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart
          data={(data ?? []).map((p) => ({ label: p.period, value: p.revenue }))}
          valueFormatter={(v) => `$${v.toLocaleString()}`}
        />
      )}
    </ReportCard>
  )
}

function AttendanceTab() {
  const { data, isLoading } = useAttendanceReport(30)

  return (
    <ReportCard title="Attendance (last 30 days)" action={<ExportButton onExport={() => exportAttendanceReport(30)} />}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart
          data={(data ?? []).map((p) => ({ label: p.date.slice(5), value: p.checkIns }))}
        />
      )}
    </ReportCard>
  )
}

function MembershipTab() {
  const { data, isLoading } = useMembershipBreakdownReport()

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <ReportCard title="Members by Status" action={<ExportButton onExport={exportMembershipReport} />}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byStatus ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
      <ReportCard title="Active Memberships by Plan Type">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byPlanType ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
    </div>
  )
}

function DataTable<T>({
  columns,
  rows,
  keyFor,
}: {
  columns: { header: string; render: (row: T) => React.ReactNode }[]
  rows: T[]
  keyFor: (row: T) => string
}) {
  if (rows.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No data for this period.</p>
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            {columns.map((c) => (
              <th key={c.header} className="py-2 pr-4">{c.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={keyFor(row)} className="border-b last:border-0">
              {columns.map((c) => (
                <td key={c.header} className="py-2 pr-4">{c.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function TrainersTab() {
  const { data, isLoading } = useTrainersList()
  const { data: commissions, isLoading: isLoadingCommissions } = useTrainerCommissionReport(6)

  return (
    <div className="space-y-4">
      <ReportCard title="Trainer Performance">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="py-2 pr-4">Trainer</th>
                  <th className="py-2 pr-4">Active Clients</th>
                  <th className="py-2 pr-4">Avg Rating</th>
                  <th className="py-2 pr-4">Commission Rate</th>
                  <th className="py-2 pr-4">Status</th>
                </tr>
              </thead>
              <tbody>
                {data?.map((t) => (
                  <tr key={t.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{t.fullName}</td>
                    <td className="py-2 pr-4">{t.activeClientCount}</td>
                    <td className="py-2 pr-4">{t.averageRating?.toFixed(1) ?? '—'}</td>
                    <td className="py-2 pr-4">{t.commissionRate}%</td>
                    <td className="py-2 pr-4">
                      <Badge variant={t.isActive ? 'default' : 'outline'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </ReportCard>

      <ReportCard
        title="Commissions (last 6 months)"
        action={<ExportButton onExport={() => exportTrainerCommissionReport(6)} />}
      >
        {isLoadingCommissions ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={commissions ?? []}
            keyFor={(r) => r.trainerName}
            columns={[
              { header: 'Trainer', render: (r) => r.trainerName },
              { header: 'Pending', render: (r) => `$${r.totalPending.toLocaleString()}` },
              { header: 'Paid', render: (r) => `$${r.totalPaid.toLocaleString()}` },
              { header: 'Records', render: (r) => r.recordCount },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function InventoryTab() {
  const { data, isLoading } = useInventoryItemsList({})
  const lowStockCount = data?.filter((i) => i.isLowStock).length ?? 0
  const { data: movements, isLoading: isLoadingMovements } = useInventoryStockMovementReport(30)

  return (
    <div className="space-y-4">
      <ReportCard title={`Inventory Levels${data ? ` — ${lowStockCount} low stock` : ''}`}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart
            data={(data ?? []).map((i) => ({ label: i.name, value: i.quantityOnHand }))}
          />
        )}
      </ReportCard>

      <ReportCard
        title="Stock Movement (last 30 days)"
        action={<ExportButton onExport={() => exportInventoryStockMovementReport(30)} />}
      >
        {isLoadingMovements ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={movements ?? []}
            keyFor={(r) => r.sku}
            columns={[
              { header: 'Item', render: (r) => r.itemName },
              { header: 'SKU', render: (r) => r.sku },
              { header: 'In', render: (r) => r.totalIn },
              { header: 'Out', render: (r) => r.totalOut },
              { header: 'Net', render: (r) => r.netChange },
              { header: 'On Hand', render: (r) => r.currentQuantityOnHand },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function EquipmentTab() {
  const { data, isLoading } = useAssetsList({})
  const counts = (data ?? []).reduce<Record<string, number>>((acc, a) => {
    acc[a.status] = (acc[a.status] ?? 0) + 1
    return acc
  }, {})
  const { data: downtime, isLoading: isLoadingDowntime } = useEquipmentDowntimeReport(6)

  return (
    <div className="space-y-4">
      <ReportCard title="Equipment Status Breakdown">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(counts).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>

      <ReportCard
        title="Downtime & Maintenance Cost (last 6 months)"
        action={<ExportButton onExport={() => exportEquipmentDowntimeReport(6)} />}
      >
        {isLoadingDowntime ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <DataTable
            rows={downtime ?? []}
            keyFor={(r) => r.assetTag}
            columns={[
              { header: 'Asset', render: (r) => r.assetName },
              { header: 'Tag', render: (r) => r.assetTag },
              { header: 'Incidents', render: (r) => r.incidents },
              { header: 'Downtime (hrs)', render: (r) => r.totalDowntimeHours.toFixed(1) },
              { header: 'Maintenance Cost', render: (r) => `$${r.totalMaintenanceCost.toLocaleString()}` },
            ]}
          />
        )}
      </ReportCard>
    </div>
  )
}

function CrmTab() {
  const { data, isLoading } = useCrmPipelineConversionReport()

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <ReportCard title="Pipeline by Stage" action={<ExportButton onExport={exportCrmPipelineReport} />}>
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <SimpleBarChart data={Object.entries(data?.byStage ?? {}).map(([label, value]) => ({ label, value }))} />
        )}
      </ReportCard>
      <ReportCard title="Conversion">
        {isLoading ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <div className="flex flex-col gap-2 text-sm">
            <p>Total leads: <span className="font-medium">{data?.totalLeads ?? 0}</span></p>
            <p>Converted to members: <span className="font-medium">{data?.convertedCount ?? 0}</span></p>
            <p>Conversion rate: <span className="font-medium">{data?.conversionRatePercent ?? 0}%</span></p>
          </div>
        )}
      </ReportCard>
    </div>
  )
}

function MaintenanceTab() {
  const { data, isLoading } = useWorkOrdersList({})
  const overdueCount = data?.filter((w) => w.isOverdue).length ?? 0
  const counts = (data ?? []).reduce<Record<string, number>>((acc, w) => {
    acc[w.status] = (acc[w.status] ?? 0) + 1
    return acc
  }, {})

  return (
    <ReportCard title={`Work Orders by Status${data ? ` — ${overdueCount} overdue` : ''}`}>
      {isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <SimpleBarChart data={Object.entries(counts).map(([label, value]) => ({ label, value }))} />
      )}
    </ReportCard>
  )
}

export default function ReportsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Reports</h1>
        <p className="text-sm text-muted-foreground">Operational insights across the gym, with Excel export.</p>
      </div>

      <Tabs defaultValue="revenue">
        <TabsList className="flex-wrap">
          <TabsTrigger value="revenue">Revenue</TabsTrigger>
          <TabsTrigger value="attendance">Attendance</TabsTrigger>
          <TabsTrigger value="membership">Membership</TabsTrigger>
          <TabsTrigger value="trainers">Trainers</TabsTrigger>
          <TabsTrigger value="inventory">Inventory</TabsTrigger>
          <TabsTrigger value="equipment">Equipment</TabsTrigger>
          <TabsTrigger value="maintenance">Maintenance</TabsTrigger>
          <TabsTrigger value="crm">CRM</TabsTrigger>
        </TabsList>

        <TabsContent value="revenue"><RevenueTab /></TabsContent>
        <TabsContent value="attendance"><AttendanceTab /></TabsContent>
        <TabsContent value="membership"><MembershipTab /></TabsContent>
        <TabsContent value="trainers"><TrainersTab /></TabsContent>
        <TabsContent value="inventory"><InventoryTab /></TabsContent>
        <TabsContent value="equipment"><EquipmentTab /></TabsContent>
        <TabsContent value="maintenance"><MaintenanceTab /></TabsContent>
        <TabsContent value="crm"><CrmTab /></TabsContent>
      </Tabs>
    </div>
  )
}

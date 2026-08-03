import { AlertTriangle } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  useMaintenanceSchedulesList,
  useUpdateWorkOrderStatus,
  useWorkOrdersList,
  type WorkOrderStatus,
} from '@/modules/maintenance/api/maintenanceApi'
import { CreateMaintenanceScheduleDialog } from '@/modules/maintenance/components/CreateMaintenanceScheduleDialog'
import { CreateWorkOrderDialog } from '@/modules/maintenance/components/CreateWorkOrderDialog'
import { useUiStore } from '@/stores/uiStore'

const STATUSES: WorkOrderStatus[] = ['Open', 'InProgress', 'Completed', 'Cancelled']

const STATUS_VARIANT: Record<WorkOrderStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Open: 'secondary',
  InProgress: 'default',
  Completed: 'outline',
  Cancelled: 'destructive',
}

const PRIORITY_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Low: 'outline',
  Medium: 'secondary',
  High: 'default',
  Critical: 'destructive',
}

function WorkOrderStatusCell({ workOrderId, status }: { workOrderId: string; status: WorkOrderStatus }) {
  const updateStatus = useUpdateWorkOrderStatus(workOrderId)

  return (
    <Select value={status} onValueChange={(v) => updateStatus.mutate({ status: v as WorkOrderStatus })}>
      <SelectTrigger size="sm" className="w-[150px]">
        <Badge variant={STATUS_VARIANT[status]}>{status}</Badge>
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

export default function MaintenancePage() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: workOrders, isLoading } = useWorkOrdersList({ branchId })
  const { data: schedules, isLoading: isLoadingSchedules } = useMaintenanceSchedulesList({ branchId })

  const overdueCount = workOrders?.filter((w) => w.isOverdue).length ?? 0

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Maintenance</h1>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            {workOrders?.length ?? '—'} work orders
            {overdueCount > 0 && (
              <span className="flex items-center gap-1 text-destructive">
                <AlertTriangle className="size-3.5" /> {overdueCount} overdue
              </span>
            )}
          </p>
        </div>
      </div>

      <Tabs defaultValue="work-orders">
        <TabsList>
          <TabsTrigger value="work-orders">Work Orders</TabsTrigger>
          <TabsTrigger value="schedules">Recurring Schedules</TabsTrigger>
        </TabsList>

        <TabsContent value="work-orders" className="space-y-3">
          <div className="flex justify-end">
            <CreateWorkOrderDialog />
          </div>
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Work Order</TableHead>
                  <TableHead>Asset</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Scheduled</TableHead>
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

                {workOrders?.map((wo) => (
                  <TableRow key={wo.id}>
                    <TableCell className="font-medium">{wo.title}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {wo.assetName} ({wo.assetTag})
                    </TableCell>
                    <TableCell className="text-muted-foreground">{wo.type}</TableCell>
                    <TableCell>
                      <Badge variant={PRIORITY_VARIANT[wo.priority]}>{wo.priority}</Badge>
                    </TableCell>
                    <TableCell className={wo.isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                      {wo.scheduledDate ? new Date(wo.scheduledDate).toLocaleDateString() : '—'}
                      {wo.isOverdue && ' (overdue)'}
                    </TableCell>
                    <TableCell>
                      <WorkOrderStatusCell workOrderId={wo.id} status={wo.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </TabsContent>

        <TabsContent value="schedules" className="space-y-3">
          <div className="flex justify-end">
            <CreateMaintenanceScheduleDialog />
          </div>
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Asset</TableHead>
                  <TableHead>Recurrence</TableHead>
                  <TableHead>Next Due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoadingSchedules &&
                  Array.from({ length: 4 }).map((_, i) => (
                    <TableRow key={i}>
                      <TableCell colSpan={4}>
                        <Skeleton className="h-6 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}

                {schedules?.length === 0 && !isLoadingSchedules && (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                      No recurring schedules yet.
                    </TableCell>
                  </TableRow>
                )}

                {schedules?.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="font-medium">{s.assetName}</TableCell>
                    <TableCell className="text-muted-foreground">{s.recurrenceRule}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(s.nextDueDate).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <Badge variant={s.isActive ? 'default' : 'outline'}>{s.isActive ? 'Active' : 'Inactive'}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}

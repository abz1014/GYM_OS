import { AlertTriangle, Wrench } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useMaintenanceSchedulesList, useWorkOrdersList, type WorkOrderStatus } from '@/modules/maintenance/api/maintenanceApi'
import { CreateMaintenanceScheduleDialog } from '@/modules/maintenance/components/CreateMaintenanceScheduleDialog'
import { CreateWorkOrderDialog } from '@/modules/maintenance/components/CreateWorkOrderDialog'
import { useUiStore } from '@/stores/uiStore'

const STATUS_VARIANT: Record<WorkOrderStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Open: 'secondary',
  InProgress: 'default',
  PendingVerification: 'outline',
  Completed: 'outline',
  Cancelled: 'destructive',
}

const PRIORITY_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Low: 'outline',
  Medium: 'secondary',
  High: 'default',
  Critical: 'destructive',
}

export default function MaintenancePage() {
  const navigate = useNavigate()
  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: workOrdersPage, isLoading } = useWorkOrdersList({ branchId, page: 1, pageSize: 100 })
  const workOrders = workOrdersPage?.items
  const { data: schedules, isLoading: isLoadingSchedules } = useMaintenanceSchedulesList({ branchId })

  const overdueCount = workOrders?.filter((w) => w.isOverdue).length ?? 0

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Maintenance</h1>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            {workOrdersPage?.totalCount ?? '—'} work orders
            {overdueCount > 0 && (
              <span className="flex items-center gap-1 text-destructive">
                <AlertTriangle className="size-3.5" /> {overdueCount} overdue
              </span>
            )}
          </p>
        </div>
      </div>

      <Tabs defaultValue="work-orders">
        <TabsList className="h-auto flex-wrap">
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
                  <TableRow
                    key={wo.id}
                    className="cursor-pointer"
                    onClick={() => navigate(`/maintenance/work-orders/${wo.id}`)}
                  >
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
                      <Badge variant={STATUS_VARIANT[wo.status]}>{wo.status}</Badge>
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
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoadingSchedules &&
                  Array.from({ length: 4 }).map((_, i) => (
                    <TableRow key={i}>
                      <TableCell colSpan={5}>
                        <Skeleton className="h-6 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}

                {schedules?.length === 0 && !isLoadingSchedules && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      No recurring schedules yet.
                    </TableCell>
                  </TableRow>
                )}

                {schedules?.map((s) => {
                  const isDue = new Date(s.nextDueDate) <= new Date()
                  return (
                    <TableRow key={s.id}>
                      <TableCell className="font-medium">{s.assetName}</TableCell>
                      <TableCell className="text-muted-foreground">{s.recurrenceRule}</TableCell>
                      <TableCell className={isDue ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                        {new Date(s.nextDueDate).toLocaleDateString()}
                        {isDue && ' (due)'}
                      </TableCell>
                      <TableCell>
                        <Badge variant={s.isActive ? 'default' : 'outline'}>{s.isActive ? 'Active' : 'Inactive'}</Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        <CreateWorkOrderDialog
                          defaultAssetId={s.assetId}
                          maintenanceScheduleId={s.id}
                          trigger={
                            <Button size="sm" variant="outline">
                              <Wrench />
                              Create Work Order
                            </Button>
                          }
                        />
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}

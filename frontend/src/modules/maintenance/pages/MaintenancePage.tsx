import { useState } from 'react'
import { AlertTriangle, Wrench } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Pagination } from '@/shared/components/Pagination'
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
  const [page, setPage] = useState(1)
  const { data: workOrdersPage, isLoading } = useWorkOrdersList({ branchId, page, pageSize: 100 })
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
          {isLoading && (
            <div className="space-y-2">
              {Array.from({ length: 8 }).map((_, i) => (
                <Skeleton key={i} className="h-24 w-full md:h-10" />
              ))}
            </div>
          )}

          {!isLoading && workOrders && workOrders.length > 0 && (
            <>
              {/* Mobile: card list */}
              <div className="space-y-2 md:hidden">
                {workOrders.map((wo) => (
                  <button
                    key={wo.id}
                    type="button"
                    onClick={() => navigate(`/maintenance/work-orders/${wo.id}`)}
                    className="block w-full space-y-1.5 rounded-lg border bg-card p-3 text-left active:bg-accent"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <p className="truncate font-medium">{wo.title}</p>
                      <Badge variant={STATUS_VARIANT[wo.status]} className="shrink-0">
                        {wo.status}
                      </Badge>
                    </div>
                    <p className="truncate text-sm text-muted-foreground">
                      {wo.assetName} ({wo.assetTag}) · {wo.type}
                    </p>
                    <div className="flex items-center justify-between gap-2 text-sm">
                      <Badge variant={PRIORITY_VARIANT[wo.priority]}>{wo.priority}</Badge>
                      <span className={wo.isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                        {wo.scheduledDate ? new Date(wo.scheduledDate).toLocaleDateString() : '—'}
                        {wo.isOverdue && ' (overdue)'}
                      </span>
                    </div>
                  </button>
                ))}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden rounded-lg border md:block">
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
                    {workOrders.map((wo) => (
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

              {workOrdersPage && (
                <Pagination
                  page={workOrdersPage.page}
                  totalPages={workOrdersPage.totalPages}
                  totalCount={workOrdersPage.totalCount}
                  hasPreviousPage={workOrdersPage.hasPreviousPage}
                  hasNextPage={workOrdersPage.hasNextPage}
                  onPageChange={setPage}
                  itemLabel="work orders"
                />
              )}
            </>
          )}
        </TabsContent>

        <TabsContent value="schedules" className="space-y-3">
          <div className="flex justify-end">
            <CreateMaintenanceScheduleDialog />
          </div>
          {isLoadingSchedules && (
            <div className="space-y-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-24 w-full md:h-10" />
              ))}
            </div>
          )}

          {schedules?.length === 0 && !isLoadingSchedules && (
            <p className="py-8 text-center text-sm text-muted-foreground">No recurring schedules yet.</p>
          )}

          {!isLoadingSchedules && schedules && schedules.length > 0 && (
            <>
              {/* Mobile: card list */}
              <div className="space-y-2 md:hidden">
                {schedules.map((s) => {
                  const isDue = new Date(s.nextDueDate) <= new Date()
                  return (
                    <div key={s.id} className="space-y-2 rounded-lg border bg-card p-3">
                      <div className="flex items-center justify-between gap-2">
                        <p className="truncate font-medium">{s.assetName}</p>
                        <Badge variant={s.isActive ? 'default' : 'outline'} className="shrink-0">
                          {s.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">{s.recurrenceRule}</p>
                      <p className={isDue ? 'text-sm font-medium text-destructive' : 'text-sm text-muted-foreground'}>
                        Next due {new Date(s.nextDueDate).toLocaleDateString()}
                        {isDue && ' (due)'}
                      </p>
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
                    </div>
                  )
                })}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden rounded-lg border md:block">
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
                    {schedules.map((s) => {
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
            </>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}

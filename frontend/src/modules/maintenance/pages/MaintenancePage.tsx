import { useState } from 'react'
import { Wrench } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'
import { Pagination } from '@/shared/components/Pagination'
import { ListEmpty, ListError, ListSkeleton, PageHeader, SEVERITY_ROW } from '@/shared/components/console'
import { useMaintenanceSchedulesList, useWorkOrdersList } from '@/modules/maintenance/api/maintenanceApi'
import { CreateMaintenanceScheduleDialog } from '@/modules/maintenance/components/CreateMaintenanceScheduleDialog'
import { CreateWorkOrderDialog } from '@/modules/maintenance/components/CreateWorkOrderDialog'
import { OverduePill, WorkOrderPriorityPill, WorkOrderStatusPill } from '@/modules/maintenance/components/WorkOrderPills'
import { useUiStore } from '@/stores/uiStore'

const dateFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

const HEAD_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

/**
 * These two tabs really do swap panels — two different endpoints, two different tables — so this
 * stays the Radix primitive rather than the console's `FilterTabs`, which exists for narrowing one
 * list that stays mounted and would throw away the tabpanel semantics and arrow-key movement that
 * a genuine panel switch needs. The classes below are only borrowing FilterTabs' underline look so
 * the two read as one control language.
 */
const TAB_TRIGGER_CLASS =
  '-mb-px h-auto flex-none rounded-none border-0 border-b-2 border-transparent bg-transparent px-0 pb-2.5 text-sm font-medium text-muted-foreground shadow-none hover:text-foreground data-[state=active]:border-foreground data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

export default function MaintenancePage() {
  const navigate = useNavigate()
  const branchId = useUiStore((s) => s.selectedBranchId)
  const [tab, setTab] = useState<'work-orders' | 'schedules'>('work-orders')
  const [page, setPage] = useState(1)

  const workOrdersQuery = useWorkOrdersList({ branchId, page, pageSize: 100 })
  const workOrdersPage = workOrdersQuery.data
  const workOrders = workOrdersPage?.items

  const schedulesQuery = useMaintenanceSchedulesList({ branchId })
  const schedules = schedulesQuery.data

  /*
   * The header used to carry "N overdue" beside the total, counted from `workOrders.filter(isOverdue)`
   * — the rows of the page you happen to be on. /api/work-orders returns no overdue or per-status
   * aggregate, so that figure was the truth about 100 rows presented as the truth about the branch,
   * and it shrank when you paged forward. `isOverdue` is a per-row flag and it is honest per row, so
   * lateness is now marked on the rows themselves and nowhere else.
   *
   * The schedules count below is a different case: /api/work-orders/schedules returns the whole list
   * unpaginated, so its length genuinely is the total.
   */
  const description =
    tab === 'work-orders'
      ? workOrdersPage
        ? `${workOrdersPage.totalCount.toLocaleString()} work orders`
        : undefined
      : schedules
        ? `${schedules.length.toLocaleString()} recurring schedules`
        : undefined

  return (
    <div className="space-y-4">
      <PageHeader
        title="Maintenance"
        description={description}
        actions={tab === 'work-orders' ? <CreateWorkOrderDialog /> : <CreateMaintenanceScheduleDialog />}
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as typeof tab)} className="gap-4">
        <TabsList className="h-auto w-full flex-wrap justify-start gap-5 rounded-none border-b border-border bg-transparent p-0">
          <TabsTrigger value="work-orders" className={TAB_TRIGGER_CLASS}>
            Work orders
          </TabsTrigger>
          <TabsTrigger value="schedules" className={TAB_TRIGGER_CLASS}>
            Recurring schedules
          </TabsTrigger>
        </TabsList>

        <TabsContent value="work-orders" className="space-y-4">
          {workOrdersQuery.isError && (
            <ListError
              message="We couldn't load the work orders"
              onRetry={() => workOrdersQuery.refetch()}
              isRetrying={workOrdersQuery.isFetching}
            />
          )}

          {workOrdersQuery.isLoading && <ListSkeleton />}

          {!workOrdersQuery.isLoading && workOrders?.length === 0 && (
            <ListEmpty message="No work orders here." hint="Nothing is booked in for repair on this branch." />
          )}

          {!workOrdersQuery.isLoading && workOrdersPage && workOrders && workOrders.length > 0 && (
            <>
              {/* Mobile: card list — asset, type, priority and a date do not fit six columns wide. */}
              <div className="space-y-2 md:hidden">
                {workOrders.map((wo) => (
                  <button
                    key={wo.id}
                    type="button"
                    onClick={() => navigate(`/maintenance/work-orders/${wo.id}`)}
                    className={cn(
                      'press block w-full space-y-1.5 rounded-panel border border-border p-3 text-left active:bg-accent',
                      // Rail and edge light are both box-shadow, so an overdue card takes the rail
                      // and the tinted ground instead of the ordinary card surface — never both.
                      wo.isOverdue ? SEVERITY_ROW.destructive : 'bg-card edge-light-soft',
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <p className="truncate font-medium">{wo.title}</p>
                      <WorkOrderStatusPill status={wo.status} className="shrink-0" />
                    </div>
                    <p className="truncate text-sm text-muted-foreground">
                      {wo.assetName} (<span className="tabular-nums">{wo.assetTag}</span>) · {wo.type}
                    </p>
                    <div className="flex items-center justify-between gap-2 text-sm">
                      <WorkOrderPriorityPill priority={wo.priority} />
                      <span className="flex items-center gap-2">
                        {wo.isOverdue && <OverduePill />}
                        <span
                          className={cn('tabular-nums', wo.isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground')}
                        >
                          {wo.scheduledDate ? dateFormat.format(new Date(wo.scheduledDate)) : '—'}
                        </span>
                      </span>
                    </div>
                  </button>
                ))}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead className={HEAD_CLASS}>Work order</TableHead>
                      <TableHead className={HEAD_CLASS}>Asset</TableHead>
                      <TableHead className={HEAD_CLASS}>Type</TableHead>
                      <TableHead className={HEAD_CLASS}>Priority</TableHead>
                      <TableHead className={HEAD_CLASS}>Scheduled</TableHead>
                      <TableHead className={HEAD_CLASS}>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {workOrders.map((wo) => (
                      <TableRow
                        key={wo.id}
                        className={cn('cursor-pointer', wo.isOverdue && SEVERITY_ROW.destructive)}
                        onClick={() => navigate(`/maintenance/work-orders/${wo.id}`)}
                      >
                        <TableCell className="font-medium">{wo.title}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {wo.assetName} (<span className="tabular-nums">{wo.assetTag}</span>)
                        </TableCell>
                        <TableCell className="text-muted-foreground">{wo.type}</TableCell>
                        <TableCell>
                          <WorkOrderPriorityPill priority={wo.priority} />
                        </TableCell>
                        <TableCell>
                          <span className="flex items-center gap-2">
                            <span
                              className={cn(
                                'tabular-nums',
                                wo.isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground',
                              )}
                            >
                              {wo.scheduledDate ? dateFormat.format(new Date(wo.scheduledDate)) : '—'}
                            </span>
                            {wo.isOverdue && <OverduePill />}
                          </span>
                        </TableCell>
                        <TableCell>
                          <WorkOrderStatusPill status={wo.status} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              <Pagination
                page={workOrdersPage.page}
                totalPages={workOrdersPage.totalPages}
                totalCount={workOrdersPage.totalCount}
                hasPreviousPage={workOrdersPage.hasPreviousPage}
                hasNextPage={workOrdersPage.hasNextPage}
                onPageChange={setPage}
                itemLabel="work orders"
              />
            </>
          )}
        </TabsContent>

        <TabsContent value="schedules" className="space-y-4">
          {schedulesQuery.isError && (
            <ListError
              message="We couldn't load the recurring schedules"
              onRetry={() => schedulesQuery.refetch()}
              isRetrying={schedulesQuery.isFetching}
            />
          )}

          {schedulesQuery.isLoading && <ListSkeleton rows={4} />}

          {!schedulesQuery.isLoading && schedules?.length === 0 && (
            <ListEmpty
              message="No recurring schedules yet."
              hint="A schedule turns a routine inspection into a work order on its own cadence."
            />
          )}

          {!schedulesQuery.isLoading && schedules && schedules.length > 0 && (
            <>
              {/* Mobile: card list */}
              <div className="space-y-2 md:hidden">
                {schedules.map((s) => {
                  // A recurring inspection that has come round is warning, not destructive: unlike an
                  // overdue work order it isn't a repair anyone is waiting on, it's the next one due.
                  const isDue = new Date(s.nextDueDate) <= new Date()
                  return (
                    <div key={s.id} className="space-y-2 rounded-panel border border-border bg-card p-3 edge-light-soft">
                      <div className="flex items-center justify-between gap-2">
                        <p className="truncate font-medium">{s.assetName}</p>
                        <span
                          className={cn(
                            'shrink-0 rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
                            s.isActive ? 'bg-success/10 text-success' : 'bg-muted text-muted-foreground',
                          )}
                        >
                          {s.isActive ? 'Active' : 'Paused'}
                        </span>
                      </div>
                      <p className="text-sm text-muted-foreground">{s.recurrenceRule}</p>
                      <p className={cn('text-sm tabular-nums', isDue ? 'font-medium text-warning' : 'text-muted-foreground')}>
                        {isDue ? 'Due since ' : 'Next due '}
                        {dateFormat.format(new Date(s.nextDueDate))}
                      </p>
                      <CreateWorkOrderDialog
                        defaultAssetId={s.assetId}
                        maintenanceScheduleId={s.id}
                        trigger={
                          <Button size="sm" variant="outline" className="rounded-xl">
                            <Wrench />
                            Create work order
                          </Button>
                        }
                      />
                    </div>
                  )
                })}
              </div>

              {/* Desktop / tablet: full table */}
              <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead className={HEAD_CLASS}>Asset</TableHead>
                      <TableHead className={HEAD_CLASS}>Recurrence</TableHead>
                      <TableHead className={HEAD_CLASS}>Next due</TableHead>
                      <TableHead className={HEAD_CLASS}>Status</TableHead>
                      <TableHead className="w-px" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {schedules.map((s) => {
                      const isDue = new Date(s.nextDueDate) <= new Date()
                      return (
                        <TableRow key={s.id}>
                          <TableCell className="font-medium">{s.assetName}</TableCell>
                          <TableCell className="text-muted-foreground">{s.recurrenceRule}</TableCell>
                          <TableCell className={cn('tabular-nums', isDue ? 'font-medium text-warning' : 'text-muted-foreground')}>
                            {dateFormat.format(new Date(s.nextDueDate))}
                            {isDue && ' · due'}
                          </TableCell>
                          <TableCell>
                            <span
                              className={cn(
                                'inline-flex items-center rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
                                s.isActive ? 'bg-success/10 text-success' : 'bg-muted text-muted-foreground',
                              )}
                            >
                              {s.isActive ? 'Active' : 'Paused'}
                            </span>
                          </TableCell>
                          <TableCell className="text-right">
                            <CreateWorkOrderDialog
                              defaultAssetId={s.assetId}
                              maintenanceScheduleId={s.id}
                              trigger={
                                <Button size="sm" variant="outline" className="rounded-xl">
                                  <Wrench />
                                  Create work order
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

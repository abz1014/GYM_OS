import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import {
  useUpdateWorkOrderStatus,
  useWorkOrder,
  type WorkOrderStatus,
} from '@/modules/maintenance/api/maintenanceApi'
import { VerifyWorkOrderDialog } from '@/modules/maintenance/components/VerifyWorkOrderDialog'

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

function WorkOrderActions({ workOrderId, status }: { workOrderId: string; status: WorkOrderStatus }) {
  const updateStatus = useUpdateWorkOrderStatus(workOrderId)

  const setStatus = (next: WorkOrderStatus) =>
    updateStatus.mutate(
      { status: next },
      { onError: () => toast.error('Could not update work order status.') }
    )

  if (status === 'Open' || status === 'InProgress') {
    return (
      <div className="flex items-center gap-2">
        {status === 'Open' && (
          <Button size="sm" disabled={updateStatus.isPending} onClick={() => setStatus('InProgress')}>
            {updateStatus.isPending && <Loader2 className="size-4 animate-spin" />}
            Start Progress
          </Button>
        )}
        {status === 'InProgress' && (
          <Button size="sm" disabled={updateStatus.isPending} onClick={() => setStatus('PendingVerification')}>
            {updateStatus.isPending && <Loader2 className="size-4 animate-spin" />}
            Submit for Verification
          </Button>
        )}
        <Button size="sm" variant="ghost" disabled={updateStatus.isPending} onClick={() => setStatus('Cancelled')}>
          Cancel
        </Button>
      </div>
    )
  }

  return null
}

export default function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: workOrder, isLoading } = useWorkOrder(id)

  if (isLoading || !workOrder) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link to="/maintenance" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to maintenance
      </Link>

      <Card>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-xl font-semibold">{workOrder.title}</h1>
                <Badge variant={STATUS_VARIANT[workOrder.status]}>{workOrder.status}</Badge>
                <Badge variant={PRIORITY_VARIANT[workOrder.priority]}>{workOrder.priority}</Badge>
              </div>
              <p className="text-sm text-muted-foreground">
                {workOrder.assetName} ({workOrder.assetTag}) · {workOrder.type}
              </p>
              {workOrder.description && <p className="mt-2 text-sm">{workOrder.description}</p>}
            </div>
            {workOrder.status === 'PendingVerification' ? (
              <VerifyWorkOrderDialog workOrderId={workOrder.id} isScheduleLinked={!!workOrder.maintenanceScheduleId} />
            ) : (
              <WorkOrderActions workOrderId={workOrder.id} status={workOrder.status} />
            )}
          </div>

          <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
            <div>
              <p className="text-muted-foreground">Scheduled</p>
              <p>{workOrder.scheduledDate ? new Date(workOrder.scheduledDate).toLocaleDateString() : '—'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Completed</p>
              <p>{workOrder.completedDate ? new Date(workOrder.completedDate).toLocaleDateString() : '—'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Cost</p>
              <p>{workOrder.cost != null ? workOrder.cost.toLocaleString('en-US', { style: 'currency', currency: 'USD' }) : '—'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Recurring schedule</p>
              <p>{workOrder.maintenanceScheduleId ? 'Linked' : '—'}</p>
            </div>
          </div>

          {workOrder.verificationNotes && (
            <div className="rounded-md border bg-muted/30 p-3 text-sm">
              <p className="font-medium">Verification notes</p>
              <p className="text-muted-foreground">{workOrder.verificationNotes}</p>
              {workOrder.verifiedAt && (
                <p className="mt-1 text-xs text-muted-foreground">{new Date(workOrder.verifiedAt).toLocaleString()}</p>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="space-y-2">
        <h2 className="text-sm font-medium">Downtime</h2>
        {workOrder.downtimeLogs.length === 0 ? (
          <p className="text-sm text-muted-foreground">No downtime recorded for this work order.</p>
        ) : (
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reason</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead>Ended</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {workOrder.downtimeLogs.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell>{d.reason ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(d.startedAt).toLocaleString()}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {d.endedAt ? new Date(d.endedAt).toLocaleString() : 'Ongoing'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </div>
  )
}

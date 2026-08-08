import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ListError, PageHeader } from '@/shared/components/console'
import { useUpdateWorkOrderStatus, useWorkOrder, type WorkOrderStatus } from '@/modules/maintenance/api/maintenanceApi'
import { VerifyWorkOrderDialog } from '@/modules/maintenance/components/VerifyWorkOrderDialog'
import { WorkOrderPriorityPill, WorkOrderStatusPill } from '@/modules/maintenance/components/WorkOrderPills'

const dateFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('en-US', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

const FIELD_LABEL_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'
const HEAD_CLASS = 'text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase'

function BackLink() {
  return (
    <Link
      to="/maintenance"
      className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
    >
      <ArrowLeft className="size-4" />
      Back to maintenance
    </Link>
  )
}

function WorkOrderActions({ workOrderId, status }: { workOrderId: string; status: WorkOrderStatus }) {
  const updateStatus = useUpdateWorkOrderStatus(workOrderId)

  const setStatus = (next: WorkOrderStatus) =>
    updateStatus.mutate({ status: next }, { onError: () => toast.error('Could not update work order status.') })

  if (status === 'Open' || status === 'InProgress') {
    return (
      <div className="flex items-center gap-2">
        {status === 'Open' && (
          <Button className="rounded-xl" disabled={updateStatus.isPending} onClick={() => setStatus('InProgress')}>
            {updateStatus.isPending && <Loader2 className="size-4 animate-spin" />}
            Start progress
          </Button>
        )}
        {status === 'InProgress' && (
          <Button className="rounded-xl" disabled={updateStatus.isPending} onClick={() => setStatus('PendingVerification')}>
            {updateStatus.isPending && <Loader2 className="size-4 animate-spin" />}
            Submit for verification
          </Button>
        )}
        <Button variant="ghost" className="rounded-xl" disabled={updateStatus.isPending} onClick={() => setStatus('Cancelled')}>
          Cancel
        </Button>
      </div>
    )
  }

  return null
}

export default function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const workOrderQuery = useWorkOrder(id)
  const workOrder = workOrderQuery.data

  // Without this branch a failed request left the page on its loading skeleton forever, because
  // `isLoading || !workOrder` cannot tell "still asking" from "asked and got a 500".
  if (workOrderQuery.isError) {
    return (
      <div className="space-y-6">
        <BackLink />
        <ListError
          message="We couldn't load this work order"
          onRetry={() => workOrderQuery.refetch()}
          isRetrying={workOrderQuery.isFetching}
        />
      </div>
    )
  }

  if (!workOrder) {
    return (
      <div className="space-y-6">
        <BackLink />
        <Skeleton className="h-10 w-64 rounded-2xl" />
        <Skeleton className="h-48 w-full rounded-2xl" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <BackLink />

      <PageHeader
        eyebrow={
          <>
            {workOrder.assetName} · <span className="tabular-nums">{workOrder.assetTag}</span>
          </>
        }
        title={workOrder.title}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <WorkOrderStatusPill status={workOrder.status} />
            <WorkOrderPriorityPill priority={workOrder.priority} />
            <span>{workOrder.type}</span>
          </span>
        }
        actions={
          workOrder.status === 'PendingVerification' ? (
            <VerifyWorkOrderDialog workOrderId={workOrder.id} isScheduleLinked={!!workOrder.maintenanceScheduleId} />
          ) : (
            <WorkOrderActions workOrderId={workOrder.id} status={workOrder.status} />
          )
        }
      />

      <div className="space-y-5 rounded-panel border border-border bg-card p-5 edge-light-soft">
        {workOrder.description && <p className="text-sm">{workOrder.description}</p>}

        {/*
          No "Assigned to" field, though the work order plainly has one. WorkOrderDetailDto carries
          assignedToUserId and nothing else — no name, and this module has no user lookup to resolve
          it against — so the row could only ever print a GUID at a technician.

          No branch or currency either, which is why the cost below is a bare figure: see the field.
        */}
        <dl className="grid grid-cols-2 gap-5 sm:grid-cols-4">
          <div>
            <dt className={FIELD_LABEL_CLASS}>Scheduled</dt>
            <dd className="mt-1.5 text-sm tabular-nums">
              {workOrder.scheduledDate ? dateFormat.format(new Date(workOrder.scheduledDate)) : '—'}
            </dd>
          </div>
          <div>
            <dt className={FIELD_LABEL_CLASS}>Completed</dt>
            <dd className="mt-1.5 text-sm tabular-nums">
              {workOrder.completedDate ? dateFormat.format(new Date(workOrder.completedDate)) : '—'}
            </dd>
          </div>
          <div>
            {/*
              The amount used to be formatted as USD. Nothing in this response says dollars —
              WorkOrderDetailDto returns a bare decimal with no currency code and no branch to look
              one up from — so the symbol was decoration on a number a manager might sign off. The
              figure is real; the currency was not, so only the figure is shown.
            */}
            <dt className={FIELD_LABEL_CLASS}>Cost</dt>
            <dd className="mt-1.5 text-sm tabular-nums">
              {workOrder.cost != null
                ? workOrder.cost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                : '—'}
            </dd>
          </div>
          <div>
            <dt className={FIELD_LABEL_CLASS}>Recurring schedule</dt>
            <dd className="mt-1.5 text-sm">{workOrder.maintenanceScheduleId ? 'Linked' : '—'}</dd>
          </div>
        </dl>

        {workOrder.verificationNotes && (
          <div className="rounded-2xl bg-muted/40 p-4 text-sm">
            <p className="font-medium">Verification notes</p>
            <p className="mt-1 text-muted-foreground">{workOrder.verificationNotes}</p>
            {workOrder.verifiedAt && (
              <p className="mt-2 text-xs text-muted-foreground tabular-nums">
                {dateTimeFormat.format(new Date(workOrder.verifiedAt))}
              </p>
            )}
          </div>
        )}
      </div>

      <section className="space-y-3">
        <h2 className="font-display text-xl font-bold tracking-tight">Downtime</h2>
        {workOrder.downtimeLogs.length === 0 ? (
          <p className="text-sm text-muted-foreground">No downtime recorded for this work order.</p>
        ) : (
          <div className="overflow-hidden rounded-panel border border-border bg-card edge-light-soft">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className={HEAD_CLASS}>Reason</TableHead>
                  <TableHead className={HEAD_CLASS}>Started</TableHead>
                  <TableHead className={HEAD_CLASS}>Ended</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {workOrder.downtimeLogs.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell>{d.reason ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground tabular-nums">
                      {dateTimeFormat.format(new Date(d.startedAt))}
                    </TableCell>
                    <TableCell
                      className={d.endedAt ? 'text-muted-foreground tabular-nums' : 'font-medium text-warning'}
                    >
                      {d.endedAt ? dateTimeFormat.format(new Date(d.endedAt)) : 'Ongoing'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </section>
    </div>
  )
}

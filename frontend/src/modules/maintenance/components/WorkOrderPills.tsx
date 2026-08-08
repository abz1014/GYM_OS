import { cn } from '@/lib/utils'
import type { WorkOrderPriority, WorkOrderStatus } from '@/modules/maintenance/api/maintenanceApi'

/**
 * The status and priority chips a work order wears, in one place because the list and the detail
 * screen both show them and had drifted into two copies of the same lookup table already.
 *
 * WorkOrderStatus is a C# enum, so "PendingVerification" is a wire value — staff read
 * "Pending verification". The colours are a queue, not a rainbow: pending verification is warm
 * because it is blocked on a manager rather than on a technician, completed is green, and open and
 * cancelled are both quiet — an open work order is normal, and a cancelled one is over. The dot
 * keeps those two apart without spending a second loud colour on either.
 */
const STATUS_LABEL: Record<WorkOrderStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  PendingVerification: 'Pending verification',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
}

export function workOrderStatusLabel(status: WorkOrderStatus): string {
  return STATUS_LABEL[status]
}

const STATUS_CHIP: Record<WorkOrderStatus, string> = {
  Open: 'bg-muted text-muted-foreground',
  InProgress: 'bg-secondary text-secondary-foreground',
  PendingVerification: 'bg-warning/10 text-warning',
  Completed: 'bg-success/10 text-success',
  Cancelled: 'bg-muted text-muted-foreground',
}

const STATUS_DOT: Record<WorkOrderStatus, string> = {
  Open: 'bg-muted-foreground',
  InProgress: 'bg-foreground',
  PendingVerification: 'bg-warning',
  Completed: 'bg-success',
  Cancelled: 'bg-destructive',
}

export function WorkOrderStatusPill({ status, className }: { status: WorkOrderStatus; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
        STATUS_CHIP[status],
        className,
      )}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', STATUS_DOT[status])} />
      {STATUS_LABEL[status]}
    </span>
  )
}

/** No dot here — priority is already an ordered scale, and two dotted chips per row is noise. */
const PRIORITY_CHIP: Record<WorkOrderPriority, string> = {
  Low: 'bg-muted text-muted-foreground',
  Medium: 'bg-secondary text-secondary-foreground',
  High: 'bg-warning/10 text-warning',
  Critical: 'bg-destructive/10 text-destructive',
}

export function WorkOrderPriorityPill({ priority, className }: { priority: WorkOrderPriority; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
        PRIORITY_CHIP[priority],
        className,
      )}
    >
      {priority}
    </span>
  )
}

/** The one thing on a work-order row that means "this is late", so it gets the loudest tone. */
export function OverduePill({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-xl bg-destructive/10 px-2.5 py-1 text-xs font-medium whitespace-nowrap text-destructive',
        className,
      )}
    >
      Overdue
    </span>
  )
}

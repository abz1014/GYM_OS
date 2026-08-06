import { cn } from '@/lib/utils'
import type { MemberStatus } from '@/modules/members/api/membersApi'

/**
 * Colour carries the meaning, so the dot is the only thing that changes between surfaces: on the
 * panel's ink header the label has to stay legible, and the light-surface success/warning hexes
 * are too dark on #0B0B0C to read at 12px. The dark variant keeps the same coloured dot and lets
 * the sidebar's own foreground token carry the text.
 *
 * Expired is warning rather than destructive on purpose — a lapsed membership is the one a front
 * desk can still win back, and colouring it the same red as a deliberate cancellation buries it.
 */
const DOT_CLASS: Record<MemberStatus, string> = {
  Active: 'bg-success',
  Frozen: 'bg-muted-foreground',
  Expired: 'bg-warning',
  Cancelled: 'bg-destructive',
}

const LIGHT_CLASS: Record<MemberStatus, string> = {
  Active: 'bg-success/10 text-success',
  Frozen: 'bg-muted text-muted-foreground',
  Expired: 'bg-warning/10 text-warning',
  Cancelled: 'bg-destructive/10 text-destructive',
}

interface MemberStatusPillProps {
  status: MemberStatus
  surface?: 'light' | 'ink'
  className?: string
}

export function MemberStatusPill({ status, surface = 'light', className }: MemberStatusPillProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-xl px-2.5 py-1 text-xs font-medium whitespace-nowrap',
        surface === 'ink' ? 'bg-sidebar-accent text-sidebar-foreground' : LIGHT_CLASS[status],
        className,
      )}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', DOT_CLASS[status])} />
      {status}
    </span>
  )
}

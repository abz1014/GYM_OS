import { CloudOff, RotateCw } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * The three things a list screen shows when it has no rows to show. They are grouped here because
 * the distinction between them is the whole point and is easy to collapse by accident: "we could not
 * reach the server", "there is genuinely nothing here", and "we are still asking" are three
 * different facts, and a list that renders "No results" while the request is still in flight — or
 * worse, while it has failed — tells staff the gym has no members.
 */

/**
 * Load failed. Always offers the retry, because the overwhelmingly common cause is a dropped
 * connection or a restarted API, and the fix is one press rather than a full page reload.
 */
export function ListError({
  message,
  onRetry,
  isRetrying,
}: {
  message: string
  onRetry: () => void
  isRetrying?: boolean
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-3xl border border-border bg-card px-4 py-12 text-center">
      <CloudOff className="size-8 text-muted-foreground" />
      <p className="font-medium">{message}</p>
      <Button variant="outline" className="rounded-xl" onClick={onRetry} disabled={isRetrying}>
        <RotateCw className={isRetrying ? 'size-4 animate-spin' : 'size-4'} />
        {isRetrying ? 'Trying…' : 'Try again'}
      </Button>
    </div>
  )
}

/**
 * Loaded fine, nothing matched. `hint` is for saying why — an active filter usually explains an
 * empty list better than the empty list does.
 */
export function ListEmpty({ message, hint }: { message: string; hint?: string }) {
  return (
    <div className="py-12 text-center">
      <p className="text-sm text-muted-foreground">{message}</p>
      {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
    </div>
  )
}

/** Still loading. Shaped like the rows it stands in for, so the page doesn't jump when they land. */
export function ListSkeleton({ rows = 8, className = 'h-16 w-full rounded-2xl md:h-12' }: { rows?: number; className?: string }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className={className} />
      ))}
    </div>
  )
}

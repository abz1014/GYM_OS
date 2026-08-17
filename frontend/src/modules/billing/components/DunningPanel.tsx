import { Link } from 'react-router-dom'
import { AlertTriangle, CreditCard } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useDunningAttempts, type DunningAttempt } from '@/modules/billing/api/dunningApi'

/**
 * Money that failed to arrive, and who to ring about it.
 *
 * THE GAP. RecurringBillingJob writes a complete dunning record — the decline reason from the
 * gateway, how many retries have burned, when the next one falls, and whether access was finally
 * suspended — and nothing in the product could read a line of it. An owner could see revenue was
 * short and had no way to find out whose card bounced. This is the weekly chase list.
 *
 * Ordered by urgency rather than date: a suspended member is losing access right now and is the
 * most likely to leave, so they come first; then whoever is closest to running out of retries.
 */
function money(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount)
  } catch {
    // An unrecognised ISO code must not blank the figure — show the number and name the currency.
    return `${amount.toFixed(2)} ${currency}`
  }
}

/** Suspended first, then fewest retries left, then largest amount. */
function byUrgency(a: DunningAttempt, b: DunningAttempt): number {
  if (a.membershipSuspended !== b.membershipSuspended) return a.membershipSuspended ? -1 : 1
  const left = (x: DunningAttempt) => x.maxAttempts - x.failedAttempts
  return left(a) - left(b) || b.amount - a.amount
}

function AttemptRows({ attempts }: { attempts: DunningAttempt[] }) {
  return (
    <>
      {attempts.map((a) => (
        <TableRow key={a.id}>
          <TableCell>
            <Link to={`/members/${a.memberId}`} className="font-medium text-primary hover:underline">
              {a.memberName}
            </Link>
            {a.membershipSuspended && (
              <Badge variant="destructive" className="ml-2">Access suspended</Badge>
            )}
          </TableCell>
          <TableCell>
            <Link to={`/billing/${a.invoiceId}`} className="text-sm text-primary hover:underline">
              {a.invoiceNumber}
            </Link>
          </TableCell>
          <TableCell className="tabular-nums">{money(a.amount, a.currency)}</TableCell>
          <TableCell className="text-sm text-muted-foreground">
            {/* The gateway's own words. "Payment failed" tells staff nothing they can act on;
                "insufficient funds" and "card expired" are two different phone calls. */}
            {a.lastFailureReason ?? 'No reason recorded'}
          </TableCell>
          <TableCell className="text-sm tabular-nums">
            {a.failedAttempts} of {a.maxAttempts}
          </TableCell>
          <TableCell className="text-sm text-muted-foreground tabular-nums">
            {a.status === 'Pending'
              ? new Date(`${a.nextAttemptDate}T00:00:00`).toLocaleDateString()
              : '—'}
          </TableCell>
        </TableRow>
      ))}
    </>
  )
}

export function DunningPanel() {
  const dunning = useDunningAttempts()

  // Succeeded attempts are history, not a chase list — the panel is only about money still missing.
  const outstanding = (dunning.data ?? []).filter((a) => a.status !== 'Succeeded').sort(byUrgency)
  const suspended = outstanding.filter((a) => a.membershipSuspended).length

  if (dunning.isLoading) return <Skeleton className="h-48 w-full" />

  if (dunning.isError) {
    return (
      <div className="rounded-panel border border-border bg-card p-6 text-center text-sm text-muted-foreground edge-light-soft">
        Couldn't load failed payments.{' '}
        <button className="underline" onClick={() => void dunning.refetch()}>Try again</button>
      </div>
    )
  }

  if (outstanding.length === 0) {
    return (
      <div className="rounded-panel border border-border bg-card p-6 text-center edge-light-soft">
        <CreditCard className="mx-auto mb-2 size-6 text-muted-foreground" aria-hidden />
        <p className="text-sm font-medium">No failed renewals</p>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Every auto-renewing membership collected successfully.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <AlertTriangle className="size-4 text-destructive" aria-hidden />
        <span className="font-medium">
          {outstanding.length} failed renewal{outstanding.length === 1 ? '' : 's'}
        </span>
        {suspended > 0 && (
          <span className="text-muted-foreground">
            · {suspended} member{suspended === 1 ? ' has' : 's have'} lost access
          </span>
        )}
      </div>

      {/* Mobile: cards, matching every other list in the console. */}
      <div className="space-y-2 md:hidden">
        {outstanding.map((a) => (
          <div key={a.id} className="space-y-1.5 rounded-panel border border-border bg-card p-3 edge-light-soft">
            <div className="flex items-start justify-between gap-2">
              <Link to={`/members/${a.memberId}`} className="min-w-0 truncate font-medium text-primary">
                {a.memberName}
              </Link>
              <span className="shrink-0 font-semibold tabular-nums">{money(a.amount, a.currency)}</span>
            </div>
            <p className="text-sm text-muted-foreground">{a.lastFailureReason ?? 'No reason recorded'}</p>
            <div className="flex flex-wrap items-center gap-1.5">
              <Badge variant="outline">Attempt {a.failedAttempts} of {a.maxAttempts}</Badge>
              {a.membershipSuspended && <Badge variant="destructive">Access suspended</Badge>}
            </div>
            <Button asChild size="sm" variant="ghost" className="press h-8 px-2">
              <Link to={`/billing/${a.invoiceId}`}>Open {a.invoiceNumber}</Link>
            </Button>
          </div>
        ))}
      </div>

      <div className="hidden overflow-hidden rounded-panel border border-border bg-card md:block edge-light-soft">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead>Invoice</TableHead>
              <TableHead>Amount</TableHead>
              <TableHead>Why it failed</TableHead>
              <TableHead>Attempts</TableHead>
              <TableHead>Next try</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <AttemptRows attempts={outstanding} />
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

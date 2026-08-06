import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { CircleCheckBig, Loader2, LogOut, RotateCw, ScanLine, SearchX, TriangleAlert, UserRound } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { useMember, useMembersList, type MemberListItem } from '@/modules/members/api/membersApi'
import { useAttendanceHistory, useCheckIn, useCheckOut } from '@/modules/attendance/api/attendanceApi'
import { initials, kioskDateFormat, kioskTimeFormat, ordinal } from '@/modules/attendance/components/frontDeskFormat'
import { useUiStore } from '@/stores/uiStore'

/** First instant of the current month, as the date-only string GET /api/attendance's fromDate wants. */
function monthStart(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
}

const STATUS_NOTE: Record<string, string> = {
  Frozen: 'Membership is frozen',
  Expired: 'Membership has expired',
  Cancelled: 'Membership is cancelled',
}

/**
 * The kiosk. One input, one verdict, both sized to be read standing on the other side of a counter
 * rather than leaning over a desk.
 *
 * Three things the redesign's mockup shows that are not built here, each because the data behind them
 * does not exist rather than because they were skipped:
 *
 * - **"Sell day pass"** — there is no day-pass, drop-in or single-visit product anywhere in this
 *   system (no domain entity, no endpoint, nothing in billing). A button that can't sell anything is
 *   worse at a counter than no button, because staff will reach for it in front of a customer.
 * - **A red "Access denied / membership expired" verdict** — POST /api/attendance/check-in performs no
 *   entitlement check at all. It resolves the member and stamps a row; the only outcome it can refuse
 *   is "no such member". This product has no door hardware and no access control, so the kiosk cannot
 *   honestly deny anybody. What it does instead is REPORT: the check-in goes through (staff decide who
 *   walks in, as they do today) and the member's real record status is shown amber beside it when it
 *   is anything other than Active. That is a fact the API actually returns; "denied" was not.
 * - **A generic failure** is still drawn, but as what it is — the request failed — not dressed up as a
 *   membership verdict we have no way to compute.
 */
export function CheckInPanel() {
  const branchId = useUiStore((s) => s.selectedBranchId)

  // What's in the field, vs. what we're actually resolving. Keeping them separate is what lets the
  // input clear itself the instant a scan is submitted while the results of that scan stay on screen.
  const [input, setInput] = useState('')
  const [lookup, setLookup] = useState('')
  const [selected, setSelected] = useState<MemberListItem | null>(null)

  const inputRef = useRef<HTMLInputElement>(null)
  // One check-in attempt per resolved member, so the mutation can't re-fire when its own cache
  // invalidation makes the dependent queries below re-settle.
  const attemptedFor = useRef<string | null>(null)

  const checkIn = useCheckIn()
  const checkOut = useCheckOut()

  /**
   * Deliberately NOT filtered to status: 'Active' the way this panel used to be. A frozen or expired
   * member still walks up to the desk, and hiding them behind "no member matches" tells staff a lie
   * about the one thing they came to this screen to find out.
   */
  const search = useMembersList({ searchTerm: lookup || undefined, page: 1, pageSize: 6 })

  const detail = useMember(selected?.id)

  // Is this person already inside? Answering before stamping a row is what stops a second scan from
  // opening a second visit and quietly inflating the "in the building" count on the rail.
  const openVisit = useAttendanceHistory(
    { branchId, memberId: selected?.id, checkedInOnly: true, page: 1, pageSize: 1 },
    { enabled: !!selected }
  )

  const monthVisits = useAttendanceHistory(
    { branchId, memberId: selected?.id, fromDate: monthStart(), page: 1, pageSize: 1 },
    { enabled: !!selected }
  )

  /**
   * A barcode scanner is a keyboard: it types into whatever holds focus and presses Enter. If focus
   * ever drifts off this field the next scan lands in nothing and the member is left standing there,
   * so the kiosk keeps taking it back — on mount, after every lookup, and after any click that isn't
   * on something interactive in its own right.
   */
  useEffect(() => {
    inputRef.current?.focus()

    const restoreFocus = (event: PointerEvent) => {
      const target = event.target as HTMLElement | null
      if (target?.closest('button, a, input, textarea, select, [role="dialog"], [role="menu"]')) return
      // Deferred, because the browser's own focus handling for this click runs after the event.
      requestAnimationFrame(() => inputRef.current?.focus())
    }

    document.addEventListener('pointerdown', restoreFocus)
    return () => document.removeEventListener('pointerdown', restoreFocus)
  }, [])

  // An unambiguous match resolves itself. Asking staff to confirm the one and only result is the
  // second click a kiosk exists to remove.
  useEffect(() => {
    if (!lookup || selected || !search.isSuccess) return
    if (search.data.items.length === 1) {
      setSelected(search.data.items[0]!)
    }
  }, [lookup, selected, search.isSuccess, search.data])

  // A scan is a decision, not a search — so the check-in fires as soon as we know who it is and know
  // they aren't already inside, rather than waiting for a second confirming tap.
  useEffect(() => {
    if (!selected || !branchId) return
    if (attemptedFor.current === selected.id) return
    if (!openVisit.isSuccess) return

    attemptedFor.current = selected.id
    if (openVisit.data.totalCount > 0) return

    checkIn.mutate(
      { memberId: selected.id, branchId, method: 'QrSimulated' },
      { onError: () => toast.error('Check-in failed.') }
    )
  }, [selected, branchId, openVisit.isSuccess, openVisit.data, checkIn])

  const reset = () => {
    setInput('')
    setLookup('')
    setSelected(null)
    attemptedFor.current = null
    checkIn.reset()
    checkOut.reset()
    inputRef.current?.focus()
  }

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    const term = input.trim()
    if (!term) return

    setSelected(null)
    attemptedFor.current = null
    checkIn.reset()
    checkOut.reset()
    setLookup(term)
    // Cleared on submit, not on success: a lookup that finds nobody has to leave the field empty too,
    // or the next scan is appended to the failed one and can never match.
    setInput('')
    inputRef.current?.focus()
  }

  const candidates = lookup && !selected && search.isSuccess ? search.data.items : []
  const totalMatches = search.data?.totalCount ?? 0
  const noMatch = !!lookup && !selected && search.isSuccess && search.data.items.length === 0

  // The open visit row is the single source for "when did this happen" in both the granted and the
  // already-inside states — the server's timestamp, never the browser's guess at one.
  const visit = openVisit.data?.items[0]
  const alreadyInside = !!visit && !checkIn.isSuccess && !checkIn.isPending
  const visitsThisMonth = monthVisits.data?.totalCount ?? 0

  /**
   * Plan and validity come from the member detail's memberships, and only when one of them is
   * genuinely Active — an expired or cancelled row has an end date too, and printing "valid until"
   * next to it at a counter would be the most dangerous kind of wrong. No active membership means the
   * line is dropped entirely; the amber status note below carries what's true instead.
   */
  const activeMembership = detail.data?.memberMemberships.find((m) => m.status === 'Active')
  const statusNote = detail.data && detail.data.status !== 'Active' ? STATUS_NOTE[detail.data.status] : undefined

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col justify-center gap-6 py-10">
      <form onSubmit={submit}>
        <label htmlFor="front-desk-scan" className="sr-only">
          Scan a card or type a member name
        </label>
        <div className="relative">
          <ScanLine className="pointer-events-none absolute top-1/2 left-6 size-7 -translate-y-1/2 text-primary" />
          <Input
            id="front-desk-scan"
            ref={inputRef}
            autoComplete="off"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Scan a card or type a name…"
            className={cn(
              'h-24 rounded-3xl border-2 border-primary bg-card pr-6 pl-18 text-2xl md:text-2xl',
              'shadow-[0_0_0_6px_color-mix(in_srgb,var(--primary)_14%,transparent)]',
              'focus-visible:border-primary focus-visible:ring-0'
            )}
          />
        </div>
      </form>

      {!branchId && (
        <p className="rounded-2xl border border-warning/30 bg-warning/10 px-5 py-4 text-lg text-warning">
          Pick a branch in the top bar before checking anyone in.
        </p>
      )}

      {/* Nothing scanned yet: the field is the whole screen, and the only copy under it says what the
          field accepts. The member search matches name, member code and email — so those are the three
          things it claims, rather than the mockup's "card scan" (see the report on qrCodeToken). */}
      {!lookup && (
        <p className="text-center text-base text-muted-foreground">
          Member name, member code or email — a counter scanner typing into this field works too.
        </p>
      )}

      {lookup && !selected && search.isPending && (
        <div className="flex items-center justify-center gap-3 py-10 text-lg text-muted-foreground">
          <Loader2 className="size-6 animate-spin" />
          Looking up “{lookup}”…
        </div>
      )}

      {search.isError && (
        <div className="flex flex-col items-center gap-3 rounded-3xl border border-border bg-card p-8 text-center">
          <TriangleAlert className="size-9 text-destructive" />
          <p className="font-display text-2xl font-black tracking-tight">Member lookup failed</p>
          <p className="text-base text-muted-foreground">The desk can't reach the member list right now.</p>
          <Button variant="outline" className="mt-1 h-12 rounded-xl px-6" onClick={() => search.refetch()}>
            <RotateCw className={search.isFetching ? 'size-4 animate-spin' : 'size-4'} />
            Try again
          </Button>
        </div>
      )}

      {noMatch && (
        <div className="flex flex-col items-center gap-3 rounded-3xl border border-border bg-card p-8 text-center">
          <SearchX className="size-9 text-muted-foreground" />
          <p className="font-display text-3xl font-black tracking-tight">No member found</p>
          <p className="text-lg text-muted-foreground">Nothing matches “{lookup}”. Try their name or member code.</p>
          <Button className="mt-2 h-14 rounded-xl px-8 text-base font-bold" onClick={reset}>
            Next
          </Button>
        </div>
      )}

      {/* More than one match is the one case a kiosk can't decide by itself, so it asks — big rows,
          because this is still being read across a counter. */}
      {candidates.length > 1 && (
        <div className="overflow-hidden rounded-3xl border border-border bg-card">
          <p className="border-b border-border px-6 py-4 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            {totalMatches > candidates.length
              ? `${totalMatches} matches — keep typing to narrow it down`
              : `${candidates.length} matches`}
          </p>
          <ul className="divide-y divide-border">
            {candidates.map((member) => (
              <li key={member.id}>
                <button
                  type="button"
                  onClick={() => setSelected(member)}
                  className="flex w-full items-center gap-4 px-6 py-4 text-left transition-colors hover:bg-accent"
                >
                  <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-muted font-display text-base font-black">
                    {initials(member.fullName)}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-display text-xl font-bold tracking-tight">{member.fullName}</span>
                    <span className="block text-sm text-muted-foreground tabular-nums">{member.memberCode}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {selected && (
        <div className="rounded-3xl border border-border bg-card p-6 sm:p-8">
          <div className="flex items-center gap-5">
            <span className="flex size-20 shrink-0 items-center justify-center rounded-2xl bg-primary font-display text-2xl font-black text-primary-foreground">
              {initials(selected.fullName)}
            </span>
            <div className="min-w-0">
              <h2 className="truncate font-display text-4xl leading-tight font-black tracking-tight sm:text-5xl">
                {selected.fullName}
              </h2>
              <p className="mt-1 text-lg text-muted-foreground">
                {activeMembership
                  ? `${activeMembership.membershipPlanName} · valid until ${kioskDateFormat.format(new Date(activeMembership.endDate))}`
                  : selected.memberCode}
              </p>
            </div>
          </div>

          {statusNote && (
            <p className="mt-5 flex items-center gap-2 rounded-2xl bg-warning/10 px-5 py-3 text-lg font-medium text-warning">
              <TriangleAlert className="size-5 shrink-0" />
              {statusNote}
            </p>
          )}

          <div className="mt-5">
            {/* The visit lookup is what decides whether this scan is a check-in at all, so if it
                fails there is no verdict to give — saying nothing here would leave the desk looking
                at a member's name with no indication anything went wrong. */}
            {openVisit.isError ? (
              <div className="rounded-2xl bg-destructive/10 px-5 py-5">
                <p className="flex items-center gap-2 font-display text-3xl font-black tracking-tight text-destructive">
                  <TriangleAlert className="size-7" />
                  Couldn't check the visit log
                </p>
                <p className="mt-1 text-lg text-muted-foreground">
                  Nothing was recorded for {selected.fullName}. Try the scan again.
                </p>
              </div>
            ) : checkIn.isPending || (!openVisit.isSuccess && !checkIn.isError) ? (
              <div className="flex items-center gap-3 rounded-2xl bg-muted px-5 py-6 text-lg text-muted-foreground">
                <Loader2 className="size-6 animate-spin" />
                Checking in…
              </div>
            ) : checkIn.isError ? (
              // Honest about what actually happened: the request failed. Not "access denied" — the
              // API never said that, and inventing a verdict staff would act on is the one thing this
              // screen must not do.
              <div className="rounded-2xl bg-destructive/10 px-5 py-5">
                <p className="flex items-center gap-2 font-display text-3xl font-black tracking-tight text-destructive">
                  <TriangleAlert className="size-7" />
                  Check-in didn't save
                </p>
                <p className="mt-1 text-lg text-muted-foreground">
                  {selected.fullName} was not recorded. Check the connection and try again.
                </p>
              </div>
            ) : checkIn.isSuccess || alreadyInside ? (
              <div
                className={cn(
                  'rounded-2xl px-5 py-5',
                  alreadyInside ? 'bg-warning/10' : 'bg-success/10'
                )}
              >
                <p
                  className={cn(
                    'flex items-center gap-2 font-display text-3xl font-black tracking-tight',
                    alreadyInside ? 'text-warning' : 'text-success'
                  )}
                >
                  <CircleCheckBig className="size-7" />
                  {alreadyInside ? 'Already inside' : 'Access granted'}
                </p>
                <p className="mt-1 text-lg text-muted-foreground tabular-nums">
                  {visit ? `Checked in at ${kioskTimeFormat.format(new Date(visit.checkInAt))}` : 'Checked in'}
                  {/* Real count, straight off the same endpoint's totalCount for this member since the
                      1st of the month — it includes the check-in that just happened because the
                      mutation invalidates the query. Rendered only once that query has actually
                      answered; a "0th visit" would be nonsense and a guess would be worse. */}
                  {monthVisits.isSuccess && visitsThisMonth > 0 && ` · ${ordinal(visitsThisMonth)} visit this month`}
                </p>
              </div>
            ) : null}
          </div>

          <div className="mt-6 flex flex-wrap gap-3">
            <Button variant="outline" asChild className="h-14 rounded-xl px-6 text-base font-bold">
              <Link to={`/members/${selected.id}`}>
                <UserRound className="size-5" />
                Open profile
              </Link>
            </Button>

            {/* Checking somebody out is the other half of the front desk's job and the reason the old
                page existed — so when the scan turns out to be a person already inside, the kiosk
                offers the action that scan probably meant instead of stamping a duplicate visit. */}
            {alreadyInside && visit && (
              <Button
                variant="outline"
                disabled={checkOut.isPending}
                onClick={() =>
                  checkOut.mutate(visit.id, {
                    onSuccess: () => {
                      toast.success(`${selected.fullName} checked out.`)
                      reset()
                    },
                    onError: () => toast.error('Check-out failed.'),
                  })
                }
                className="h-14 rounded-xl px-6 text-base font-bold"
              >
                {checkOut.isPending ? <Loader2 className="size-5 animate-spin" /> : <LogOut className="size-5" />}
                Check out
              </Button>
            )}

            <Button onClick={reset} className="ml-auto h-14 rounded-xl px-10 text-base font-bold">
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

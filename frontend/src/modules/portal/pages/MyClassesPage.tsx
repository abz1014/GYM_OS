import { useLayoutEffect, useMemo, useState } from 'react'
import { CalendarCheck, Check, ChevronDown, History, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import type { ClassBookingStatus } from '@/modules/classes/api/classesApi'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
import {
  useBookMyClass,
  useCancelMyClassBooking,
  useMyClassBookings,
  useMyClassHistory,
  useMyClassSchedule,
  type MyClassSession,
} from '@/modules/portal/api/portalApi'
import { Pagination } from '@/shared/components/Pagination'
import { isStale } from '@/shared/lib/queryTrust'

// Sessions store their start as wall-clock-in-UTC — format in UTC so the class time reads the same
// for every member regardless of their device time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' })
const dayFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

/** Below this many free places, the remaining count is the reason to book now rather than trivia. */
const NEARLY_FULL_SPOTS = 5

/**
 * How many days of bookable classes are on screen at once.
 *
 * GetMyClassScheduleQuery filters on `StartsAt >= now` with no horizon and no Take, and this page
 * rendered every row it returned. So the density did not depend on the design at all — it depended
 * on how far ahead somebody happened to generate the timetable. A branch with a full weekly schedule
 * generated a month out produces hundreds of rows in one scroll.
 *
 * A fortnight was the first cut at that and was still too much: a daily timetable is fourteen day
 * headings and something like fifty rows, which is the same wall of classes, just a shorter wall.
 * Five days is about what a member plans around.
 *
 * The window counts DAYS THAT HAVE CLASSES, not calendar days. Those are the same thing at a gym
 * running a daily timetable, and where they differ — a branch with Mon/Wed/Fri classes — counting
 * calendar days would put two days of content on one page and produce empty pages later. Nobody
 * wants to press Next to see nothing.
 */
const DAYS_PER_PAGE = 5

function ClassAction({ session, waitlistPosition }: { session: MyClassSession; waitlistPosition: number | null }) {
  const book = useBookMyClass()
  const cancel = useCancelMyClassBooking()

  const handleBook = () =>
    book.mutate(session.sessionId, {
      onSuccess: (status) =>
        toast.success(status === 'Waitlisted' ? "You're on the waitlist." : "You're booked in!"),
      onError: () => toast.error('Could not book this class.'),
    })

  const handleCancel = () =>
    cancel.mutate(session.myBookingId!, {
      onSuccess: () => toast.success('Booking cancelled.'),
      onError: () => toast.error('Could not cancel your booking.'),
    })

  const busy = book.isPending || cancel.isPending

  if (session.myBookingStatus === 'Booked' || session.myBookingStatus === 'CheckedIn') {
    return (
      <div className="flex shrink-0 flex-col items-end gap-1">
        <span className="flex items-center gap-1.5 rounded-xl bg-primary px-3 py-2 text-sm font-bold text-primary-foreground shadow-volt">
          <Check className="size-4" />
          {session.myBookingStatus === 'CheckedIn' ? 'Checked in' : 'Booked'}
        </span>
        {session.myBookingStatus === 'Booked' && (
          <Button size="sm" variant="ghost" className="h-7 px-2 text-xs text-muted-foreground" disabled={busy} onClick={handleCancel}>
            Cancel
          </Button>
        )}
      </div>
    )
  }

  if (session.myBookingStatus === 'Waitlisted') {
    return (
      <div className="flex shrink-0 flex-col items-end gap-1">
        {/* "Waitlisted" is the same word for first in line and fourteenth, and a member reads it as
            "I'm not getting in" either way — first in line at a gym class usually does. The position
            is appended only when the bookings call actually returned one; a missing position leaves
            the plain word rather than guessing at #1. */}
        <span className="rounded-xl border border-border px-3 py-2 text-sm font-bold text-muted-foreground">
          {waitlistPosition === null ? 'Waitlisted' : `Waitlisted · #${waitlistPosition}`}
        </span>
        <Button size="sm" variant="ghost" className="h-7 px-2 text-xs text-muted-foreground" disabled={busy} onClick={handleCancel}>
          Leave
        </Button>
      </div>
    )
  }

  return (
    <Button
      variant={session.isFull ? 'outline' : 'secondary'}
      className={cn(
        'h-11 shrink-0 rounded-xl px-4 font-bold',
        !session.isFull && 'bg-accent text-accent-foreground hover:bg-accent/80',
      )}
      disabled={busy}
      onClick={handleBook}
    >
      {busy && <Loader2 className="size-4 animate-spin" />}
      {session.isFull ? 'Waitlist' : 'Book'}
    </Button>
  )
}

/**
 * How full a class is, on the card rather than a tap away — fullness IS the booking decision, and a
 * member choosing between two classes is choosing on whether they'll get in.
 *
 * The bar's colour carries the same judgement the caption states, so the row is readable at a glance
 * without reading the number: green with room, amber when it's nearly gone, destructive when full.
 */
function Capacity({ session }: { session: MyClassSession }) {
  const spotsLeft = Math.max(0, session.capacity - session.bookedCount)
  const nearlyFull = !session.isFull && spotsLeft <= NEARLY_FULL_SPOTS

  /*
   * One encoding, not two.
   *
   * This rendered a proportional bar AND a caption of the same ratio, side by side — the same fact
   * said twice in one row, which is a good part of why the page read as congested. The words carry
   * it: "Full", "3 left", "12/20" are all more precise than a 56px bar, and colour still does the
   * at-a-glance work the bar was there for.
   */
  return (
    <span
      className={cn(
        'mt-1.5 block truncate text-xs whitespace-nowrap',
        session.isFull ? 'font-medium text-destructive' : nearlyFull ? 'font-medium text-warning' : 'text-muted-foreground',
      )}
    >
      {/* The design's "Full · 4 on waitlist" needs a waitlist size the schedule endpoint does not
          return — only whether the member is on it themselves. "Full" is the part that is true. */}
      {session.isFull
        ? 'Full'
        : nearlyFull
          ? `${spotsLeft} left`
          : `${session.bookedCount}/${session.capacity}`}
    </span>
  )
}

/** One class. Booked state is signalled once — by the action on the right, which also says what to
 *  do about it. It used to be signalled three times: a card tint, a coloured time, and a pill. */
function SessionRow({ session, waitlistPosition }: { session: MyClassSession; waitlistPosition: number | null }) {
  const booked = session.myBookingStatus === 'Booked' || session.myBookingStatus === 'CheckedIn'

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-2xl border bg-card p-3 edge-light',
        booked ? 'border-primary/40' : 'border-border',
      )}
    >
      {/* Time leads: a member scanning a day is scanning for when, then deciding on what. */}
      <div className="w-16 shrink-0 border-r border-border pr-3">
        <p className="font-display text-lg font-black tabular-nums">{timeFmt.format(new Date(session.startsAt))}</p>
        <p className="text-xs text-muted-foreground">{session.durationMinutes} min</p>
      </div>

      <div className="min-w-0 flex-1">
        <p className="truncate font-display text-base font-bold">{session.classTypeName}</p>
        <p className="truncate text-sm text-muted-foreground">
          {[session.trainerName, session.location].filter(Boolean).join(' · ') || 'No instructor'}
        </p>
        <Capacity session={session} />
      </div>

      <ClassAction session={session} waitlistPosition={waitlistPosition} />
    </div>
  )
}

function DayGroups({
  sessions,
  waitlistPositions,
}: {
  sessions: MyClassSession[]
  /** sessionId -> queue position. Empty when the bookings call hasn't landed, which is why the chip
   *  treats a missing entry as "no position known" rather than as position zero. */
  waitlistPositions: Map<string, number>
}) {
  const grouped = sessions.reduce<Record<string, MyClassSession[]>>((acc, s) => {
    const key = s.startsAt.slice(0, 10)
    ;(acc[key] ??= []).push(s)
    return acc
  }, {})

  return (
    <>
      {Object.entries(grouped).map(([date, daySessions]) => (
        <section key={date} className="space-y-2">
          <h3 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            {dayFmt.format(new Date(`${date}T00:00:00Z`))}
          </h3>
          {daySessions.map((s) => (
            <SessionRow key={s.sessionId} session={s} waitlistPosition={waitlistPositions.get(s.sessionId) ?? null} />
          ))}
        </section>
      ))}
    </>
  )
}

/**
 * A finished class, as a verdict rather than an intention.
 *
 * Only three of the five booking statuses can survive into history, but all five are mapped: a
 * status with no entry here would render as the raw enum name, and "NoShow" is a developer's word.
 */
const HISTORY_STATUS: Record<ClassBookingStatus, { label: string; variant: 'success' | 'secondary' | 'outline' | 'destructive' }> = {
  CheckedIn: { label: 'Attended', variant: 'success' },
  NoShow: { label: 'No-show', variant: 'destructive' },
  Booked: { label: 'Booked', variant: 'secondary' },
  Waitlisted: { label: 'Waitlisted', variant: 'outline' },
  Cancelled: { label: 'Cancelled', variant: 'outline' },
}

const historyDayFmt = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  month: 'short',
  day: 'numeric',
  timeZone: 'UTC',
})

/**
 * What they've already done, folded away by default.
 *
 * Collapsed because this screen exists to answer "what am I in and what can I join" — a member's
 * class history is a thing they go looking for (was I marked down as a no-show?), not something to
 * scroll past on the way to booking. Expanded state is local and deliberately not remembered: the
 * default question this page answers is about the future.
 */
function PastClasses() {
  const [open, setOpen] = useState(false)
  const history = useMyClassHistory()

  return (
    <div className="space-y-2">
      <Button
        variant="ghost"
        className="h-11 w-full justify-between rounded-xl px-3"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <span className="flex items-center gap-2 font-display text-base font-bold">
          <History className="size-4 text-muted-foreground" />
          Past classes
          {/* The count comes from real rows, so it appears only once they've arrived. */}
          {history.data && (
            <span className="text-sm font-semibold text-muted-foreground tabular-nums">{history.data.length}</span>
          )}
        </span>
        <ChevronDown className={cn('size-4 shrink-0 transition-transform', open && 'rotate-180')} />
      </Button>

      {open &&
        (history.isLoading ? (
          <Skeleton className="h-24 w-full rounded-2xl shimmer" />
        ) : isStale(history) ? (
          <MemberLoadError
            title="We couldn't load your past classes"
            hint="Every class you've been to is still on your record."
            onRetry={() => void history.refetch()}
            isRetrying={history.isFetching}
          />
        ) : history.data && history.data.length > 0 ? (
          <ul className="space-y-2">
            {history.data.map((entry, i) => (
              /* No id on a history row, so the key is composed from what makes it unique — the
                 class and the minute it started. The index is the tiebreak and nothing more. */
              <li
                key={`${entry.startsAt}-${entry.classTypeName}-${i}`}
                className="flex items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3"
              >
                <div className="min-w-0">
                  <p className="truncate font-medium">{entry.classTypeName}</p>
                  <p className="text-xs text-muted-foreground">
                    {historyDayFmt.format(new Date(entry.startsAt))} · {entry.durationMinutes} min
                  </p>
                </div>
                <Badge variant={HISTORY_STATUS[entry.status].variant} className="shrink-0">
                  {HISTORY_STATUS[entry.status].label}
                </Badge>
              </li>
            ))}
          </ul>
        ) : (
          <p className="px-3 py-6 text-center text-sm text-muted-foreground">
            You haven't been to a class here yet.
          </p>
        ))}
    </div>
  )
}

/**
 * Two questions, asked separately: what am I already in, and what else could I join.
 *
 * This was one undifferentiated list of every future session at the branch with the member's own
 * bookings scattered through it, so "when is my next class" and "what is on Thursday" were the same
 * scroll. Splitting them costs nothing — one query still, filtered twice — and turns the first
 * question into a glance.
 */
export default function MyClassesPage() {
  const schedule = useMyClassSchedule()
  /*
   * A second call purely for the waitlist positions.
   *
   * /api/me/classes says WHETHER the member is on a waitlist; only /api/me/class-bookings says where
   * in it. They are joined on sessionId here rather than the schedule being asked to carry the
   * position, because the schedule is the branch's timetable and the queue position is personal.
   * This query failing costs the "#N" suffix and nothing else — the chip and the Leave button both
   * come from the schedule — so it deliberately does not raise a load error of its own.
   */
  const bookings = useMyClassBookings()
  const [page, setPage] = useState(1)

  const waitlistPositions = useMemo(() => {
    const map = new Map<string, number>()
    for (const b of bookings.data ?? []) {
      if (b.waitlistPosition !== null) map.set(b.sessionId, b.waitlistPosition)
    }
    return map
  }, [bookings.data])

  const { mine, browse, totalDays, totalPages } = useMemo(() => {
    const all = schedule.data ?? []
    const isMine = (s: MyClassSession) => s.myBookingStatus !== null
    const bookable = all.filter((s) => !isMine(s))

    // Distinct days, in order. The schedule arrives sorted by start time, so the first appearance of
    // each date is already in the right place and no comparator is needed.
    const days: string[] = []
    for (const s of bookable) {
      const day = s.startsAt.slice(0, 10)
      if (days.at(-1) !== day) days.push(day)
    }

    const pages = Math.max(1, Math.ceil(days.length / DAYS_PER_PAGE))
    const visible = new Set(days.slice((page - 1) * DAYS_PER_PAGE, page * DAYS_PER_PAGE))

    return {
      // Never paginated. A member's own commitments are the one thing a window must not hide, and
      // there are only ever a handful of them.
      mine: all.filter(isMine),
      browse: bookable.filter((s) => visible.has(s.startsAt.slice(0, 10))),
      totalDays: days.length,
      totalPages: pages,
    }
  }, [schedule.data, page])

  /*
   * A refetch can shorten the schedule — a session ends, the last day on the final page empties —
   * and leave `page` pointing past the end, which renders as a blank list with a Previous button.
   * Clamping in a layout effect corrects it before the empty frame is ever painted.
   */
  useLayoutEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const ready = !schedule.isLoading && !isStale(schedule)

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="font-display text-3xl font-black tracking-tight">Classes</h1>
        <p className="text-sm text-muted-foreground">What you're booked into, and what else is on.</p>
      </div>

      {/* Was a bare sentence with no retry — the only member screen not using this convention. It
          also blamed the member's account ("ask the front desk if your membership is linked to your
          login") for what is almost always a dropped request. */}
      {isStale(schedule) && (
        <MemberLoadError
          title="We couldn't load the class schedule"
          hint="Any class you've booked is still booked."
          onRetry={() => void schedule.refetch()}
          isRetrying={schedule.isFetching}
        />
      )}

      {schedule.isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-24 w-full rounded-2xl shimmer" />
          ))}
        </div>
      )}

      {ready && mine.length > 0 && (
        <div className="space-y-2">
          <h2 className="font-display text-lg font-black tracking-tight">
            Your classes
            <span className="ml-2 text-sm font-semibold text-muted-foreground tabular-nums">{mine.length}</span>
          </h2>
          {/* The actual rule, stated once, where the Cancel button is. The app enforces exactly this
              and nothing more — no cut-off window, no penalty — and a member who doesn't know that
              hedges by not booking. */}
          <p className="text-xs text-muted-foreground">
            You can cancel a booking any time before the class starts.
          </p>
          <DayGroups sessions={mine} waitlistPositions={waitlistPositions} />
        </div>
      )}

      {ready && (
        <div className="space-y-2">
          {mine.length > 0 && <h2 className="font-display text-lg font-black tracking-tight">Book a class</h2>}

          {browse.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-10 text-center text-sm text-muted-foreground">
              <CalendarCheck className="size-6" />
              {mine.length > 0
                ? 'Nothing else on at your gym right now.'
                : 'No upcoming classes at your gym right now.'}
            </div>
          ) : (
            <>
              <DayGroups sessions={browse} waitlistPositions={waitlistPositions} />
              {/*
                Counted in DAYS, not sessions, because days are what the pages are made of — saying
                "60 classes" beside a control that moves five days at a time invites the member to
                work out how many presses that is.
              */}
              <Pagination
                page={page}
                totalPages={totalPages}
                totalCount={totalDays}
                hasPreviousPage={page > 1}
                hasNextPage={page < totalPages}
                onPageChange={(next) => {
                  setPage(next)
                  // Paging without this leaves the member at the bottom of the previous page,
                  // looking at the last rows of a day they have already scrolled past.
                  window.scrollTo({ top: 0, behavior: 'smooth' })
                }}
                itemLabel={totalDays === 1 ? 'day' : 'days'}
              />
            </>
          )}
        </div>
      )}

      {/* Last on the page, and behind a tap: the future is what this screen is for. */}
      <PastClasses />
    </div>
  )
}

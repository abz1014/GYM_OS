import { useMemo, useState } from 'react'
import { CalendarCheck, Check, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
import {
  useBookMyClass,
  useCancelMyClassBooking,
  useMyClassSchedule,
  type MyClassSession,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

// Sessions store their start as wall-clock-in-UTC — format in UTC so the class time reads the same
// for every member regardless of their device time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' })
const dayFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

/** Below this many free places, the remaining count is the reason to book now rather than trivia. */
const NEARLY_FULL_SPOTS = 5

/**
 * How far ahead "Book a class" reaches before the member has to ask for more.
 *
 * GetMyClassScheduleQuery filters on `StartsAt >= now` with no horizon and no Take, and this page
 * rendered every row it returned. So the density did not depend on the design at all — it depended
 * on how far ahead somebody happened to generate the timetable. A branch with a full weekly schedule
 * generated a month out produces hundreds of rows in one scroll.
 */
const DEFAULT_HORIZON_DAYS = 14

function ClassAction({ session }: { session: MyClassSession }) {
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
        <span className="rounded-xl border border-border px-3 py-2 text-sm font-bold text-muted-foreground">
          Waitlisted
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
function SessionRow({ session }: { session: MyClassSession }) {
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

      <ClassAction session={session} />
    </div>
  )
}

function DayGroups({ sessions }: { sessions: MyClassSession[] }) {
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
            <SessionRow key={s.sessionId} session={s} />
          ))}
        </section>
      ))}
    </>
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
  const [showAll, setShowAll] = useState(false)

  const { mine, browse, hidden } = useMemo(() => {
    const all = schedule.data ?? []
    const isMine = (s: MyClassSession) => s.myBookingStatus !== null

    const cutoff = new Date()
    cutoff.setDate(cutoff.getDate() + DEFAULT_HORIZON_DAYS)
    const bookable = all.filter((s) => !isMine(s))
    const near = bookable.filter((s) => new Date(s.startsAt) <= cutoff)

    return {
      // Never truncated. A member's own commitments are the one thing a horizon must not hide, and
      // there are only ever a handful of them.
      mine: all.filter(isMine),
      browse: showAll ? bookable : near,
      hidden: showAll ? 0 : bookable.length - near.length,
    }
  }, [schedule.data, showAll])

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
          <DayGroups sessions={mine} />
        </div>
      )}

      {ready && (
        <div className="space-y-2">
          {mine.length > 0 && <h2 className="font-display text-lg font-black tracking-tight">Book a class</h2>}

          {browse.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-10 text-center text-sm text-muted-foreground">
              <CalendarCheck className="size-6" />
              {mine.length > 0
                ? `Nothing else on in the next ${DEFAULT_HORIZON_DAYS} days.`
                : 'No upcoming classes at your gym right now.'}
            </div>
          ) : (
            <DayGroups sessions={browse} />
          )}

          {hidden > 0 && (
            <Button variant="outline" className="press h-11 w-full rounded-2xl" onClick={() => setShowAll(true)}>
              Show {hidden} more further ahead
            </Button>
          )}
        </div>
      )}
    </div>
  )
}

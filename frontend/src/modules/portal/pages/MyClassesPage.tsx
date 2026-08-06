import { CalendarCheck, Check, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import {
  useBookMyClass,
  useCancelMyClassBooking,
  useMyClassSchedule,
  type MyClassSession,
} from '@/modules/portal/api/portalApi'

// Sessions store their start as wall-clock-in-UTC — format in UTC so the class time reads the same
// for every member regardless of their device time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' })
const dayFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

/** Below this many free places, the remaining count is the reason to book now rather than trivia. */
const NEARLY_FULL_SPOTS = 5

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
        <span className="flex items-center gap-1.5 rounded-xl bg-primary px-3 py-2 text-sm font-bold text-primary-foreground">
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
  const fraction = session.capacity > 0 ? Math.min(1, session.bookedCount / session.capacity) : 0
  const nearlyFull = !session.isFull && spotsLeft <= NEARLY_FULL_SPOTS

  return (
    <div className="mt-1.5 flex items-center gap-2">
      <span className="h-1.5 w-14 shrink-0 overflow-hidden rounded-full bg-border">
        <span
          className={cn(
            'block h-full rounded-full transition-[width] duration-700 ease-out',
            session.isFull ? 'bg-destructive' : nearlyFull ? 'bg-warning' : 'bg-success',
          )}
          style={{ width: `${fraction * 100}%` }}
        />
      </span>
      {/* nowrap because this is one fact — wrapped over three lines it stops reading as a status and
          starts reading as a layout accident. */}
      <span
        className={cn(
          'truncate text-xs whitespace-nowrap',
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
    </div>
  )
}

export default function MyClassesPage() {
  const { data: sessions, isLoading, isError } = useMyClassSchedule()

  const grouped = (sessions ?? []).reduce<Record<string, MyClassSession[]>>((acc, s) => {
    const key = s.startsAt.slice(0, 10)
    ;(acc[key] ??= []).push(s)
    return acc
  }, {})

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="font-display text-3xl font-black tracking-tight">Classes</h1>
        <p className="text-sm text-muted-foreground">Book your spot in this week's group classes.</p>
      </div>

      {isError && (
        <p className="text-sm text-muted-foreground">
          We couldn't load the class schedule. Ask the front desk if your membership is linked to your login.
        </p>
      )}

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-24 w-full rounded-2xl" />
          ))}
        </div>
      )}

      {!isLoading && sessions?.length === 0 && (
        <div className="flex flex-col items-center gap-2 py-10 text-center text-sm text-muted-foreground">
          <CalendarCheck className="size-6" />
          No upcoming classes at your gym right now.
        </div>
      )}

      {!isLoading &&
        Object.entries(grouped).map(([date, daySessions]) => (
          <section key={date} className="space-y-2">
            <h2 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
              {dayFmt.format(new Date(`${date}T00:00:00Z`))}
            </h2>
            {daySessions.map((s) => {
              const booked = s.myBookingStatus === 'Booked' || s.myBookingStatus === 'CheckedIn'
              return (
                <div
                  key={s.sessionId}
                  className={cn(
                    'flex items-center gap-3 rounded-2xl border bg-card p-3',
                    // A class the member is already in reads as settled before anything is read.
                    booked ? 'border-primary/40 bg-accent' : 'border-border',
                  )}
                >
                  {/* Time leads: a member scanning a day is scanning for when, then deciding on what. */}
                  <div className="w-16 shrink-0 border-r border-border pr-3">
                    <p className={cn('font-display text-lg font-black tabular-nums', booked && 'text-primary')}>
                      {timeFmt.format(new Date(s.startsAt))}
                    </p>
                    <p className="text-xs text-muted-foreground">{s.durationMinutes} min</p>
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-display text-base font-bold">{s.classTypeName}</p>
                    <p className="truncate text-sm text-muted-foreground">
                      {[s.trainerName, s.location].filter(Boolean).join(' · ') || 'No instructor'}
                    </p>
                    <Capacity session={s} />
                  </div>

                  <ClassAction session={s} />
                </div>
              )
            })}
          </section>
        ))}
    </div>
  )
}

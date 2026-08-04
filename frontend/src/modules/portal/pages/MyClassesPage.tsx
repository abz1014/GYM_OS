import { CalendarCheck, Clock, Loader2, MapPin, Users } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  useBookMyClass,
  useCancelMyClassBooking,
  useMyClassSchedule,
  type MyClassSession,
} from '@/modules/portal/api/portalApi'

// Sessions store their start as wall-clock-in-UTC — format in UTC so the class time reads the same
// for every member regardless of their device time zone.
const timeFmt = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' })
const dayFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'short', day: 'numeric', timeZone: 'UTC' })

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
      <div className="flex flex-col items-end gap-1">
        <Badge className="shrink-0">{session.myBookingStatus === 'CheckedIn' ? 'Checked in' : 'Booked'}</Badge>
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
      <div className="flex flex-col items-end gap-1">
        <Badge variant="secondary" className="shrink-0">Waitlisted</Badge>
        <Button size="sm" variant="ghost" className="h-7 px-2 text-xs text-muted-foreground" disabled={busy} onClick={handleCancel}>
          Leave
        </Button>
      </div>
    )
  }

  return (
    <Button size="sm" variant={session.isFull ? 'outline' : 'default'} className="shrink-0" disabled={busy} onClick={handleBook}>
      {busy && <Loader2 className="size-4 animate-spin" />}
      {session.isFull ? 'Join waitlist' : 'Book'}
    </Button>
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
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Classes</h1>
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
            <Skeleton key={i} className="h-20 w-full" />
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
          <div key={date} className="space-y-2">
            <h2 className="text-sm font-semibold text-muted-foreground">{dayFmt.format(new Date(`${date}T00:00:00Z`))}</h2>
            {daySessions.map((s) => (
              <Card key={s.sessionId}>
                <CardContent className="flex items-center justify-between gap-3 p-3">
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <span
                        className="size-2.5 shrink-0 rounded-full"
                        style={{ backgroundColor: s.colorHex ?? 'var(--muted-foreground)' }}
                      />
                      <span className="truncate font-medium">{s.classTypeName}</span>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1">
                        <Clock className="size-3" />
                        {timeFmt.format(new Date(s.startsAt))} · {s.durationMinutes} min
                      </span>
                      <span className="flex items-center gap-1">
                        <Users className="size-3" />
                        {s.bookedCount}/{s.capacity}
                      </span>
                      {s.location && (
                        <span className="flex items-center gap-1">
                          <MapPin className="size-3" />
                          {s.location}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground">{s.trainerName ?? 'No instructor'}</p>
                  </div>
                  <ClassAction session={s} />
                </CardContent>
              </Card>
            ))}
          </div>
        ))}
    </div>
  )
}

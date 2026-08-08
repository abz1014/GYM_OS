import { useState } from 'react'
import { CheckCircle2, UserMinus, UserPlus, XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { ListSkeleton, SearchField } from '@/shared/components/console'
import { useMembersList } from '@/modules/members/api/membersApi'
import {
  useBookClassSession,
  useCancelClassBooking,
  useClassSessionRoster,
  useRecordClassBookingAttendance,
  type ClassBookingStatus,
  type ClassSession,
} from '@/modules/classes/api/classesApi'

const STATUS_VARIANT: Record<ClassBookingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Booked: 'default',
  CheckedIn: 'default',
  Waitlisted: 'secondary',
  NoShow: 'destructive',
  Cancelled: 'outline',
}

const STATUS_LABEL: Record<ClassBookingStatus, string> = {
  Booked: 'Booked',
  CheckedIn: 'Checked in',
  Waitlisted: 'Waitlisted',
  NoShow: 'No-show',
  Cancelled: 'Cancelled',
}

function MemberSearch({ sessionId }: { sessionId: string }) {
  const [searchTerm, setSearchTerm] = useState('')
  const { data: members } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })
  const book = useBookClassSession(sessionId)

  const handleBook = (memberId: string, name: string) => {
    book.mutate(memberId, {
      onSuccess: (status) => {
        toast.success(status === 'Waitlisted' ? `${name} added to the waitlist.` : `${name} booked in.`)
        setSearchTerm('')
      },
      onError: () => toast.error('Could not book this member.'),
    })
  }

  return (
    <div className="space-y-2">
      <SearchField
        value={searchTerm}
        onChange={setSearchTerm}
        placeholder="Name, code, phone or email"
        aria-label="Search a member to book in"
      />
      {searchTerm && (
        <div className="divide-y divide-border overflow-hidden rounded-2xl border border-border">
          {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
          {members?.items.map((m) => (
            <button
              key={m.id}
              type="button"
              disabled={book.isPending}
              onClick={() => handleBook(m.id, m.fullName)}
              className="flex w-full items-center justify-between px-3 py-2.5 text-left text-sm hover:bg-accent disabled:opacity-50"
            >
              <span className="truncate">{m.fullName}</span>
              <span className="flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground tabular-nums">
                {m.memberCode}
                <UserPlus className="size-3.5" />
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export function ClassRosterDialog({ session }: { session: ClassSession }) {
  const [open, setOpen] = useState(false)
  const { data: roster, isLoading } = useClassSessionRoster(open ? session.id : undefined)
  const cancelBooking = useCancelClassBooking(session.id)
  const recordAttendance = useRecordClassBookingAttendance(session.id)

  const isCancelledSession = session.status === 'Cancelled'

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {/* The booked-against-capacity figure moved onto the session row itself, where it sits with
            the time and the room; this is now just the way in. */}
        <Button size="sm" variant="outline" className="shrink-0 rounded-xl">
          Roster
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="font-display tracking-tight">{session.classTypeName} — roster</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <p className="text-sm text-muted-foreground tabular-nums">
            {roster ? `${roster.bookedCount}/${roster.capacity} booked` : `${session.bookedCount}/${session.capacity} booked`}
            {(roster?.waitlistCount ?? session.waitlistCount) > 0 &&
              ` · ${roster?.waitlistCount ?? session.waitlistCount} waitlisted`}
          </p>

          {!isCancelledSession && <MemberSearch sessionId={session.id} />}

          {isLoading && <ListSkeleton rows={4} className="h-12 w-full rounded-2xl" />}

          {roster && roster.bookings.length === 0 && (
            <p className="py-4 text-center text-sm text-muted-foreground">No bookings yet.</p>
          )}

          {roster && roster.bookings.length > 0 && (
            <div className="divide-y divide-border overflow-hidden rounded-2xl border border-border">
              {roster.bookings.map((b) => {
                const isWaitlisted = b.status === 'Waitlisted'
                return (
                  <div key={b.id} className="flex items-center justify-between gap-2 px-3 py-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{b.memberName}</p>
                      <p className="text-xs text-muted-foreground tabular-nums">{b.memberCode}</p>
                    </div>
                    <div className="flex items-center gap-1">
                      <Badge variant={STATUS_VARIANT[b.status]}>{STATUS_LABEL[b.status]}</Badge>
                      {b.status === 'Booked' && (
                        <>
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Check in"
                            className="rounded-xl"
                            disabled={recordAttendance.isPending}
                            onClick={() => recordAttendance.mutate({ bookingId: b.id, attended: true })}
                          >
                            <CheckCircle2 className="size-4 text-success" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Mark no-show"
                            className="rounded-xl"
                            disabled={recordAttendance.isPending}
                            onClick={() => recordAttendance.mutate({ bookingId: b.id, attended: false })}
                          >
                            <XCircle className="size-4 text-destructive" />
                          </Button>
                        </>
                      )}
                      {(b.status === 'Booked' || isWaitlisted) && (
                        <Button
                          size="icon"
                          variant="ghost"
                          title="Cancel booking"
                          className="rounded-xl"
                          disabled={cancelBooking.isPending}
                          onClick={() => cancelBooking.mutate(b.id)}
                        >
                          <UserMinus className="size-4 text-muted-foreground" />
                        </Button>
                      )}
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

import { useState } from 'react'
import { CheckCircle2, Loader2, UserMinus, UserPlus, XCircle } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
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
      <Input
        placeholder="Search a member to book..."
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
      />
      {searchTerm && (
        <div className="divide-y rounded-md border">
          {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
          {members?.items.map((m) => (
            <button
              key={m.id}
              type="button"
              disabled={book.isPending}
              onClick={() => handleBook(m.id, m.fullName)}
              className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent disabled:opacity-50"
            >
              <span>{m.fullName}</span>
              <span className="flex items-center gap-1 text-xs text-muted-foreground">
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
        <Button size="sm" variant="outline" className="shrink-0">
          {session.bookedCount}/{session.capacity}
          {session.waitlistCount > 0 && ` · +${session.waitlistCount}`}
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {session.classTypeName} — roster
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {roster ? `${roster.bookedCount}/${roster.capacity} booked` : `${session.bookedCount}/${session.capacity} booked`}
            {(roster?.waitlistCount ?? session.waitlistCount) > 0 &&
              ` · ${roster?.waitlistCount ?? session.waitlistCount} waitlisted`}
          </p>

          {!isCancelledSession && <MemberSearch sessionId={session.id} />}

          {isLoading && (
            <div className="space-y-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          )}

          {roster && roster.bookings.length === 0 && (
            <p className="py-4 text-center text-sm text-muted-foreground">No bookings yet.</p>
          )}

          {roster && roster.bookings.length > 0 && (
            <div className="divide-y rounded-md border">
              {roster.bookings.map((b) => {
                const isWaitlisted = b.status === 'Waitlisted'
                return (
                  <div key={b.id} className="flex items-center justify-between gap-2 px-3 py-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{b.memberName}</p>
                      <p className="text-xs text-muted-foreground">{b.memberCode}</p>
                    </div>
                    <div className="flex items-center gap-1">
                      <Badge variant={STATUS_VARIANT[b.status]}>{STATUS_LABEL[b.status]}</Badge>
                      {b.status === 'Booked' && (
                        <>
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Check in"
                            disabled={recordAttendance.isPending}
                            onClick={() => recordAttendance.mutate({ bookingId: b.id, attended: true })}
                          >
                            <CheckCircle2 className="size-4 text-success" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Mark no-show"
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

          {isLoading && <Loader2 className="mx-auto size-4 animate-spin text-muted-foreground" />}
        </div>
      </DialogContent>
    </Dialog>
  )
}

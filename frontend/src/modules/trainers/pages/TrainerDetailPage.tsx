import type { ReactNode } from 'react'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, Star } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ListEmpty, ListError, PageHeader, StatTile } from '@/shared/components/console'
import { useTrainer, useUpdateCommissionStatus } from '@/modules/trainers/api/trainersApi'
import { AddCommissionRecordDialog } from '@/modules/trainers/components/AddCommissionRecordDialog'
import { AddTrainerRatingDialog } from '@/modules/trainers/components/AddTrainerRatingDialog'
import { AddTrainerScheduleDialog } from '@/modules/trainers/components/AddTrainerScheduleDialog'
import { AssignClientDialog } from '@/modules/trainers/components/AssignClientDialog'
import { EndAssignmentButton } from '@/modules/trainers/components/EndAssignmentButton'
import { ScheduleSessionDialog } from '@/modules/trainers/components/ScheduleSessionDialog'
import { SessionActionButtons } from '@/modules/trainers/components/SessionActionButtons'
import { useAuthStore } from '@/stores/authStore'

// USD because TrainerDetail carries no currency code — only a branchId, which this page does not
// resolve to a branch. Same assumption the page has always made, kept in one place.
const currency = (amount: number) => amount.toLocaleString('en-US', { style: 'currency', currency: 'USD' })

const COMMISSION_STATUSES = ['Pending', 'Paid'] as const

/** Two letters at most, matching the roster cards on the list screen. */
function initials(fullName: string): string {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join('')
}

/**
 * Paid/Pending — a dropdown for whoever can settle commission, the badge alone otherwise. Same
 * shape as the equipment status cell, and for the same reason: a `<Select>` bound to server data
 * that 403s just snaps back, so it has to be absent rather than merely ineffective.
 */
function CommissionStatusCell({
  trainerId,
  commissionRecordId,
  status,
}: {
  trainerId: string
  commissionRecordId: string
  status: string
}) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const updateStatus = useUpdateCommissionStatus(trainerId)

  if (!hasPermission('trainers.manage')) {
    return <Badge variant={status === 'Paid' ? 'success' : 'outline'}>{status}</Badge>
  }

  return (
    <Select
      value={status}
      onValueChange={(v) => updateStatus.mutate({ commissionRecordId, status: v as 'Pending' | 'Paid' })}
    >
      <SelectTrigger size="sm" className="w-[110px] rounded-xl">
        <Badge variant={status === 'Paid' ? 'success' : 'outline'}>{status}</Badge>
      </SelectTrigger>
      <SelectContent>
        {COMMISSION_STATUSES.map((s) => (
          <SelectItem key={s} value={s}>
            {s}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function BackToTrainers() {
  return (
    <Link to="/trainers" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
      <ArrowLeft className="size-4" />
      Back to trainers
    </Link>
  )
}

/** The row shell every tab panel repeats: one card per assignment, session, slot, rating or record. */
function Row({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-panel border border-border bg-card p-4 edge-light-soft">
      {children}
    </div>
  )
}

export default function TrainerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const trainerQuery = useTrainer(id)
  const trainer = trainerQuery.data

  /*
   * Every mutation this page can fire — assigning a client, ending an assignment, booking or
   * completing a session, adding a slot, a rating or a commission record, settling one — sits behind
   * trainers.manage. Only the two GETs are trainers.view. A Trainer holds the second and not the
   * first, so this page reads as a profile for them and as a console for a manager.
   */
  const canManage = hasPermission('trainers.manage')

  // Without this branch a failed request left the skeleton on screen for good, since `isLoading ||
  // !trainer` cannot tell a request still in flight from one that has already given up.
  if (trainerQuery.isError) {
    return (
      <div className="space-y-4">
        <BackToTrainers />
        <ListError
          message="We couldn't load this trainer"
          onRetry={() => trainerQuery.refetch()}
          isRetrying={trainerQuery.isFetching}
        />
      </div>
    )
  }

  if (trainerQuery.isLoading || !trainer) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full rounded-2xl" />
      </div>
    )
  }

  /*
   * All four tiles count rows the detail endpoint returns in full, so they are exact rather than
   * page-scoped. Two of them will read zero on a fresh install — this system seeds no PT sessions
   * and no commission records — and that zero is the answer, not a missing value dressed up as one.
   *
   * There is deliberately no rating tile. GET /api/trainers/{id} returns the individual ratings but
   * no aggregate — averageRating is computed by GET /api/trainers, for the roster cards. Averaging
   * the rows here would be a second implementation of a figure the backend already owns, free to
   * drift from it the day that definition changes. The ratings tab lists every score instead.
   */
  const activeClients = trainer.assignments.filter((a) => a.isActive).length
  const endedClients = trainer.assignments.length - activeClients
  const completedSessions = trainer.sessions.filter((s) => s.status === 'Completed').length
  const scheduledSessions = trainer.sessions.filter((s) => s.status === 'Scheduled').length

  return (
    <div className="space-y-6">
      <BackToTrainers />

      <PageHeader
        eyebrow={trainer.specialties}
        title={`${trainer.firstName} ${trainer.lastName}`}
        description={trainer.email}
        actions={!trainer.isActive ? <Badge variant="secondary">Inactive</Badge> : undefined}
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="Active clients"
          value={activeClients.toLocaleString()}
          caption={endedClients > 0 ? `${endedClients} ended` : undefined}
        />
        <StatTile label="Commission rate" value={`${trainer.commissionRate}%`} />
        <StatTile
          label="Total earned"
          value={currency(trainer.totalCommissionEarned)}
          caption={
            trainer.commissionRecords.length > 0
              ? `Across ${trainer.commissionRecords.length} ${trainer.commissionRecords.length === 1 ? 'record' : 'records'}`
              : undefined
          }
        />
        <StatTile
          label="Sessions completed"
          value={completedSessions.toLocaleString()}
          caption={scheduledSessions > 0 ? `${scheduledSessions} still scheduled` : undefined}
        />
      </div>

      {trainer.bio && (
        <div className="rounded-panel border border-border bg-card p-5 edge-light-soft">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Bio</p>
          <p className="mt-2 text-sm">{trainer.bio}</p>
        </div>
      )}

      {/* Radix tabs, not the console's FilterTabs: these swap five panels rather than narrowing one
          list, so they keep the panel semantics and take only the new radii. */}
      <Tabs defaultValue="clients" className="gap-4">
        <TabsList className="h-auto flex-wrap rounded-xl">
          <TabsTrigger value="clients" className="rounded-lg">
            Clients
          </TabsTrigger>
          <TabsTrigger value="sessions" className="rounded-lg">
            Sessions
          </TabsTrigger>
          <TabsTrigger value="schedule" className="rounded-lg">
            Schedule
          </TabsTrigger>
          <TabsTrigger value="ratings" className="rounded-lg">
            Ratings
          </TabsTrigger>
          <TabsTrigger value="commissions" className="rounded-lg">
            Commissions
          </TabsTrigger>
        </TabsList>

        <TabsContent value="clients" className="space-y-3">
          {canManage && (
            <div className="flex justify-end">
              <AssignClientDialog trainerId={trainer.id} />
            </div>
          )}
          {trainer.assignments.length === 0 && (
            <ListEmpty
              message="No clients assigned yet."
              hint={canManage ? 'Assign a member to start booking sessions.' : undefined}
            />
          )}
          {trainer.assignments.map((a) => (
            <Row key={a.id}>
              <div className="flex min-w-0 items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary font-display text-sm font-black">
                  {initials(a.memberName)}
                </span>
                <div className="min-w-0">
                  <p className="truncate font-medium">{a.memberName}</p>
                  <p className="text-xs text-muted-foreground tabular-nums">
                    Since {new Date(a.startDate).toLocaleDateString()}
                    {a.endDate && ` · ended ${new Date(a.endDate).toLocaleDateString()}`}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={a.isActive ? 'success' : 'outline'}>{a.isActive ? 'Active' : 'Ended'}</Badge>
                {canManage && a.isActive && <EndAssignmentButton trainerId={trainer.id} assignmentId={a.id} />}
              </div>
            </Row>
          ))}
        </TabsContent>

        <TabsContent value="sessions" className="space-y-3">
          {canManage && (
            <div className="flex justify-end">
              <ScheduleSessionDialog trainerId={trainer.id} assignments={trainer.assignments} />
            </div>
          )}
          {/* Empty on a fresh install and correct: nothing seeds PT sessions, so this panel is the
              honest picture of a trainer nobody has booked yet. */}
          {trainer.sessions.length === 0 && (
            <ListEmpty
              message="No sessions scheduled yet."
              hint={canManage ? 'Book one against an active client assignment.' : undefined}
            />
          )}
          {trainer.sessions.map((s) => (
            <Row key={s.id}>
              <div className="min-w-0">
                <p className="truncate font-medium">{s.memberName}</p>
                <p className="text-sm text-muted-foreground tabular-nums">
                  {new Date(s.scheduledAt).toLocaleString()} · {s.durationMinutes} min
                </p>
                {s.notes && <p className="text-sm text-muted-foreground">{s.notes}</p>}
              </div>
              <div className="flex items-center gap-2">
                <Badge
                  variant={s.status === 'Completed' ? 'success' : s.status === 'Cancelled' ? 'secondary' : 'outline'}
                >
                  {s.status}
                </Badge>
                {canManage && s.status === 'Scheduled' && (
                  <SessionActionButtons trainerId={trainer.id} sessionId={s.id} />
                )}
              </div>
            </Row>
          ))}
        </TabsContent>

        <TabsContent value="schedule" className="space-y-3">
          {canManage && (
            <div className="flex justify-end">
              <AddTrainerScheduleDialog trainerId={trainer.id} />
            </div>
          )}
          {trainer.schedules.length === 0 && (
            <ListEmpty
              message="No schedule set."
              hint={canManage ? 'Add the weekly slots this trainer works.' : undefined}
            />
          )}
          {trainer.schedules.map((s) => (
            <Row key={s.id}>
              <span className="font-medium">{s.dayOfWeek}</span>
              <span className="text-sm text-muted-foreground tabular-nums">
                {s.startTime} – {s.endTime}
              </span>
              <Badge variant={s.isAvailable ? 'success' : 'secondary'}>
                {s.isAvailable ? 'Available' : 'Unavailable'}
              </Badge>
            </Row>
          ))}
        </TabsContent>

        <TabsContent value="ratings" className="space-y-3">
          {canManage && (
            <div className="flex justify-end">
              <AddTrainerRatingDialog
                trainerId={trainer.id}
                assignments={trainer.assignments}
                sessions={trainer.sessions}
              />
            </div>
          )}
          {trainer.ratings.length === 0 && (
            <ListEmpty
              message="No ratings yet."
              hint={canManage ? 'Clients can be rated against a completed session, or in general.' : undefined}
            />
          )}
          {trainer.ratings.map((r) => (
            <div key={r.id} className="space-y-1.5 rounded-panel border border-border bg-card p-4 edge-light-soft">
              <div className="flex items-center justify-between gap-3">
                <span className="truncate font-medium">{r.memberName}</span>
                <span className="flex shrink-0 items-center gap-1 text-warning tabular-nums">
                  <Star className="size-3.5 fill-current" /> {r.score}/5
                </span>
              </div>
              {r.comment && <p className="text-sm text-muted-foreground">{r.comment}</p>}
              {r.sessionId && <p className="text-xs text-muted-foreground">For a completed session</p>}
            </div>
          ))}
        </TabsContent>

        <TabsContent value="commissions" className="space-y-3">
          {canManage && (
            <div className="flex justify-end">
              <AddCommissionRecordDialog trainerId={trainer.id} />
            </div>
          )}
          {/* Also empty on a fresh install: no commission record is ever seeded, and one only exists
              once somebody records a period by hand. */}
          {trainer.commissionRecords.length === 0 && (
            <ListEmpty
              message="No commission records yet."
              hint={canManage ? 'Record a period to start tracking what is owed.' : undefined}
            />
          )}
          {trainer.commissionRecords.map((c) => (
            <Row key={c.id}>
              <span className="text-sm tabular-nums">
                {new Date(c.period).toLocaleDateString(undefined, { year: 'numeric', month: 'long' })}
              </span>
              <span className="font-display text-lg font-bold tabular-nums">{currency(c.amount)}</span>
              <CommissionStatusCell trainerId={trainer.id} commissionRecordId={c.id} status={c.status} />
            </Row>
          ))}
        </TabsContent>
      </Tabs>
    </div>
  )
}

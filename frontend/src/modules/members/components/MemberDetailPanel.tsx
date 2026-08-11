import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Apple,
  ArrowUpRight,
  Cake,
  CloudOff,
  CreditCard,
  DoorOpen,
  Dumbbell,
  Loader2,
  Mail,
  MapPin,
  MessageSquare,
  Phone,
  QrCode,
  Receipt,
  RotateCw,
  Ruler,
  StickyNote,
  TriangleAlert,
  UserPlus,
  X,
  type LucideIcon,
} from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'
import { anyStale, isStale } from '@/shared/lib/queryTrust'
import { useAuthStore } from '@/stores/authStore'
import { useCheckIn } from '@/modules/attendance/api/attendanceApi'
import {
  useBranches,
  useMember,
  useMemberInvoices,
  useMemberTimeline,
  useMemberVisitCountSince,
  useMemberVisits,
  type MemberMembership,
  type MemberTimelineEntry,
} from '@/modules/members/api/membersApi'
import { useMemberWorkoutLogs } from '@/modules/workouts/api/workoutsApi'
import { MemberStatusPill } from '@/modules/members/components/MemberStatusPill'
import { AddEmergencyContactDialog } from '@/modules/members/components/AddEmergencyContactDialog'
import { AddMeasurementDialog } from '@/modules/members/components/AddMeasurementDialog'
import { AddMedicalNoteDialog } from '@/modules/members/components/AddMedicalNoteDialog'
import { AddProgressPhotoDialog } from '@/modules/members/components/AddProgressPhotoDialog'
import { CancelMembershipDialog } from '@/modules/members/components/CancelMembershipDialog'
import { EditMemberDialog } from '@/modules/members/components/EditMemberDialog'
import { FreezeMembershipDialog } from '@/modules/members/components/FreezeMembershipDialog'
import { ReactivateMembershipButton } from '@/modules/members/components/ReactivateMembershipButton'
import { RenewMembershipDialog } from '@/modules/members/components/RenewMembershipDialog'
import { ResumeMembershipButton } from '@/modules/members/components/ResumeMembershipButton'
import { TransferMemberDialog } from '@/modules/members/components/TransferMemberDialog'

const monthYearFormat = new Intl.DateTimeFormat('en-US', { month: 'short', year: 'numeric' })
const dayMonthFormat = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })
const timeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })

const MS_PER_DAY = 86_400_000

/** DateOnly wire values ("2026-08-27") are calendar dates — parse them as local midnight, not UTC. */
function parseDateOnly(value: string): Date {
  return new Date(`${value.slice(0, 10)}T00:00:00`)
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate())
}

function daysBetween(from: Date, to: Date): number {
  return Math.round((startOfDay(to).getTime() - startOfDay(from).getTime()) / MS_PER_DAY)
}

function money(amount: number, currency: string): string {
  return amount.toLocaleString('en-US', { style: 'currency', currency })
}

/**
 * Staff scan this for recency, not for a timestamp — the exact date only matters once it's old.
 *
 * `withTime` is off for anything whose source recorded a date and no time. Printing "Today, 5:00 AM"
 * beside a measurement that only ever knew "2026-08-11" states an hour nobody recorded, and staff
 * read it as fact.
 */
function relativeDay(value: Date, withTime = true): string {
  const days = daysBetween(value, new Date())
  if (days <= 0) return withTime ? `Today, ${timeFormat.format(value)}` : 'Today'
  if (days === 1) return 'Yesterday'
  if (days < 30) return `${days} days ago`
  return dayMonthFormat.format(value)
}

/**
 * When to show a timeline entry as happening.
 *
 * A date-only entry arrives as midnight UTC. Read naively it drifts by the viewer's offset — east of
 * UTC it grows a small-hours time, west of it the whole entry slides to the previous day — so its
 * calendar date is rebuilt from the UTC parts and rendered without an hour.
 */
function timelineWhen(entry: MemberTimelineEntry): string {
  const at = new Date(entry.at)
  if (!entry.isDateOnly) return relativeDay(at)
  return relativeDay(new Date(at.getUTCFullYear(), at.getUTCMonth(), at.getUTCDate()), false)
}

function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase()
}

/**
 * MemberMembershipStatus has six values against MemberStatus's four, so this can't reuse
 * MemberStatusPill — Transferred and PendingActivation have no member-level equivalent.
 */
const MEMBERSHIP_TONE: Record<string, string> = {
  Active: 'bg-success/10 text-success',
  Frozen: 'bg-muted text-muted-foreground',
  PendingActivation: 'bg-primary/15 text-foreground',
  Expired: 'bg-warning/10 text-warning',
  Cancelled: 'bg-destructive/10 text-destructive',
  Transferred: 'bg-muted text-muted-foreground',
}

function MembershipBadge({ status }: { status: string }) {
  return (
    <span className={cn('rounded-lg px-2 py-1 text-xs font-medium', MEMBERSHIP_TONE[status] ?? 'bg-muted text-muted-foreground')}>
      {status === 'PendingActivation' ? 'Pending' : status}
    </span>
  )
}

function Eyebrow({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <p className={cn('text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase', className)}>{children}</p>
  )
}

/**
 * The plan a staff member is being asked about is the one running now — an Active membership, or
 * the frozen/pending one that will be. Only if none of those exist does the newest expired or
 * cancelled record stand in, because "no plan at all" and "plan ended in March" are different
 * answers to the same question.
 */
function currentMembership(memberships: MemberMembership[]): MemberMembership | undefined {
  return (
    memberships.find((m) => m.status === 'Active') ??
    memberships.find((m) => m.status === 'Frozen') ??
    memberships.find((m) => m.status === 'PendingActivation') ??
    [...memberships].sort((a, b) => parseDateOnly(b.endDate).getTime() - parseDateOnly(a.endDate).getTime())[0]
  )
}

/**
 * Which module an event came from, as a colour. Deliberately the same tones the rest of the panel
 * already uses for these things — green for a visit, red for money owed — so the timeline reads as
 * the screens it was assembled from rather than a new vocabulary.
 */
const TIMELINE_TONE: Record<string, string> = {
  Visit: 'bg-success/10 text-success',
  Workout: 'bg-primary/15 text-foreground',
  Nutrition: 'bg-success/10 text-success',
  Invoice: 'bg-muted text-muted-foreground',
  Payment: 'bg-success/10 text-success',
  Membership: 'bg-primary/15 text-foreground',
  Measurement: 'bg-muted text-muted-foreground',
  Coaching: 'bg-warning/10 text-warning',
}

const TIMELINE_ICON: Record<string, LucideIcon> = {
  Visit: DoorOpen,
  Workout: Dumbbell,
  Nutrition: Apple,
  Invoice: Receipt,
  Payment: CreditCard,
  Membership: CreditCard,
  Measurement: Ruler,
  Coaching: MessageSquare,
}

function TimelineRow({ entry }: { entry: MemberTimelineEntry }) {
  const Icon = TIMELINE_ICON[entry.kind] ?? StickyNote
  return (
    <li className="flex items-start gap-3">
      <span
        className={cn(
          'flex size-8 shrink-0 items-center justify-center rounded-xl',
          TIMELINE_TONE[entry.kind] ?? 'bg-muted text-muted-foreground',
        )}
      >
        <Icon className="size-4" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{entry.title}</p>
        <p className="text-xs text-muted-foreground">
          {timelineWhen(entry)}
          {entry.detail ? ` · ${entry.detail}` : ''}
        </p>
      </div>
    </li>
  )
}

function StatTile({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-border px-3 py-2.5">
      <Eyebrow className="text-[10px]">{label}</Eyebrow>
      <p className="mt-1 font-display text-xl leading-none font-black tracking-tight tabular-nums">{value}</p>
    </div>
  )
}

interface ActivityEntry {
  id: string
  at: Date
  icon: React.ReactNode
  title: string
  detail?: string
}

function ActivityIcon({ children, tone }: { children: React.ReactNode; tone: string }) {
  return <span className={cn('flex size-8 shrink-0 items-center justify-center rounded-xl', tone)}>{children}</span>
}

interface MemberDetailPanelProps {
  memberId: string
  /**
   * 'rail' is the master-detail rail on the list page — it owns a close control and a way out to
   * the full route. 'page' is that full route (/members/:id), which is reached by deep link, by a
   * row click on a screen too narrow for a rail, and by every existing bookmark.
   */
  variant: 'rail' | 'page'
  onClose?: () => void
}

export function MemberDetailPanel({ memberId, variant, onClose }: MemberDetailPanelProps) {
  const memberQuery = useMember(memberId)
  const member = memberQuery.data

  const hasPermission = useAuthStore((s) => s.hasPermission)
  const canSeeVisits = hasPermission('attendance.view')
  const canSeeBilling = hasPermission('billing.view')
  const canCheckIn = hasPermission('attendance.check_in')
  /*
   * Two permissions cover every write this panel offers, and they split it cleanly:
   * members.update owns the record — the profile, measurements, photos, medical notes, emergency
   * contacts — and members.manage_membership owns the plan, which is renew, freeze, resume, cancel,
   * reactivate and transfer. Trainer and Nutritionist read members and hold neither, so this panel
   * was showing both of them a full set of controls that 403'd. Each one is now absent instead.
   */
  const canUpdate = hasPermission('members.update')
  const canManageMembership = hasPermission('members.manage_membership')

  // Fixed 30-day window, expressed as a date so the query key is stable across a render rather
  // than a new timestamp each time.
  const thirtyDaysAgo = useMemo(() => new Date(Date.now() - 30 * MS_PER_DAY).toISOString().slice(0, 10), [])

  const visitsQuery = useMemberVisits(memberId, canSeeVisits)
  const visits30dQuery = useMemberVisitCountSince(memberId, thirtyDaysAgo, canSeeVisits)
  const invoicesQuery = useMemberInvoices(memberId, canSeeBilling)
  const branchesQuery = useBranches()

  /*
   * Session history for the Training tab. Gated on workouts.view — the endpoint's own permission —
   * rather than fetched-and-403'd: a receptionist doesn't hold it, and for them the tab keeps its
   * measurements and photos, which members.view already covers.
   */
  const canSeeWorkouts = hasPermission('workouts.view')
  const workoutLogsQuery = useMemberWorkoutLogs(canSeeWorkouts ? memberId : undefined)

  // Only fetched while the History tab is open. It is the widest query on the panel and most visits
  // to a member never ask for it.
  const [timelineOpened, setTimelineOpened] = useState(false)
  const timelineQuery = useMemberTimeline(memberId, timelineOpened)

  const checkIn = useCheckIn()

  const branchName = branchesQuery.data?.find((b) => b.id === member?.branchId)?.name
  const lastVisit = visitsQuery.data?.items[0]
  const lastVisitAt = lastVisit ? new Date(lastVisit.checkInAt) : null

  /**
   * "In the gym" means an open check-in from today. Without the same-day guard, one historic
   * record that never got a check-out would park a member in the building indefinitely — and this
   * chip is what decides whether staff are offered the check-in button at all.
   */
  const openVisitToday = lastVisitAt && !lastVisit?.checkOutAt && daysBetween(lastVisitAt, new Date()) === 0 ? lastVisitAt : null

  const activity = useMemo(() => {
    if (!member) return []
    const entries: ActivityEntry[] = []

    for (const visit of visitsQuery.data?.items ?? []) {
      entries.push({
        id: `visit-${visit.id}`,
        at: new Date(visit.checkInAt),
        icon: (
          <ActivityIcon tone="bg-success/10 text-success">
            <DoorOpen className="size-4" />
          </ActivityIcon>
        ),
        title: 'Checked in',
        detail: visit.method === 'Manual' ? 'Recorded at the desk' : 'QR scan',
      })
    }

    for (const invoice of invoicesQuery.data?.items ?? []) {
      entries.push({
        id: `invoice-${invoice.id}`,
        // Invoices carry an issue DATE, not a timestamp, so same-day ordering against check-ins
        // is approximate. Showing the invoice is still worth more than hiding it for that.
        at: parseDateOnly(invoice.issueDate),
        icon: (
          <ActivityIcon
            tone={
              invoice.status === 'Paid'
                ? 'bg-success/10 text-success'
                : invoice.status === 'Overdue'
                  ? 'bg-destructive/10 text-destructive'
                  : 'bg-muted text-muted-foreground'
            }
          >
            <Receipt className="size-4" />
          </ActivityIcon>
        ),
        title: `Invoice ${invoice.invoiceNumber} · ${invoice.status}`,
        detail: money(invoice.totalAmount, invoice.currency),
      })
    }

    for (const membership of member.memberMemberships) {
      entries.push({
        id: `membership-${membership.id}`,
        at: parseDateOnly(membership.startDate),
        icon: (
          <ActivityIcon tone="bg-primary/15 text-foreground">
            <CreditCard className="size-4" />
          </ActivityIcon>
        ),
        title: `${membership.membershipPlanName} started`,
        detail: money(membership.pricePaid, membership.currency),
      })
    }

    for (const note of member.medicalNotes) {
      entries.push({
        id: `note-${note.id}`,
        at: new Date(note.recordedAt),
        // The DTO carries recordedByUserId but no name, so this can't say who wrote it the way
        // the design's "Note from Maya S." does — one unresolved id is not a person's name.
        icon: (
          <ActivityIcon tone="bg-warning/10 text-warning">
            <StickyNote className="size-4" />
          </ActivityIcon>
        ),
        title: 'Medical note added',
        detail: note.note,
      })
    }

    return entries.sort((a, b) => b.at.getTime() - a.at.getTime()).slice(0, 8)
  }, [member, visitsQuery.data, invoicesQuery.data])

  // Only sources this account is allowed to see count as failures — a receptionist without
  // billing.view never fires the invoices query, and a disabled query must not read as a broken one.
  const activityFeedFailed = anyStale(canSeeVisits && visitsQuery, canSeeBilling && invoicesQuery)

  // A paused query is stuck at isLoading forever, so an isError-only branch leaves these two
  // sections spinning through an outage instead of admitting they have nothing.
  const workoutsUntrustworthy = anyStale(workoutLogsQuery)
  const timelineUntrustworthy = anyStale(timelineQuery)

  if (isStale(memberQuery)) {
    return (
      <div className="overflow-hidden rounded-3xl border border-border bg-card">
        <div className="flex flex-col items-center gap-3 px-5 py-12 text-center">
          <CloudOff className="size-8 text-muted-foreground" />
          <p className="font-medium">We couldn't load this member</p>
          <Button
            variant="outline"
            className="rounded-xl"
            onClick={() => memberQuery.refetch()}
            disabled={memberQuery.isFetching}
          >
            <RotateCw className={memberQuery.isFetching ? 'size-4 animate-spin' : 'size-4'} />
            {memberQuery.isFetching ? 'Trying…' : 'Try again'}
          </Button>
        </div>
      </div>
    )
  }

  if (!member) {
    return (
      <div className="overflow-hidden rounded-3xl border border-border bg-card">
        <div className="bg-sidebar p-5">
          <Skeleton className="h-14 w-full rounded-2xl opacity-20" />
        </div>
        <div className="space-y-3 p-5">
          <Skeleton className="h-24 w-full rounded-2xl" />
          <Skeleton className="h-16 w-full rounded-2xl" />
          <Skeleton className="h-40 w-full rounded-2xl" />
        </div>
      </div>
    )
  }

  const plan = currentMembership(member.memberMemberships)
  const planEnd = plan ? parseDateOnly(plan.endDate) : null
  const planStart = plan ? parseDateOnly(plan.startDate) : null
  const daysRemaining = planEnd ? daysBetween(new Date(), planEnd) : null
  const planTotalDays = planStart && planEnd ? Math.max(daysBetween(planStart, planEnd), 1) : null
  const planProgress =
    planTotalDays !== null && daysRemaining !== null
      ? Math.min(Math.max(1 - daysRemaining / planTotalDays, 0), 1)
      : null

  const handleCheckIn = () => {
    checkIn.mutate(
      // The member's own branch, not the branch chosen in the top bar: this panel is opened from a
      // directory that spans every branch the user can see, and a member can only be checked in
      // where they belong.
      { memberId: member.id, branchId: member.branchId, method: 'Manual' },
      {
        onSuccess: () => toast.success(`${member.firstName} checked in.`),
        onError: () => toast.error('Check-in failed.'),
      },
    )
  }

  return (
    <div className="overflow-hidden rounded-3xl border border-border bg-card">
      {/*
        Ink header — the one dark block on a light screen, per the redesign.

        Except on the front desk, which is the single staff screen that runs dark, and where the
        trick stops working: --sidebar is DEFINED as --background there (#0B0B0C both), so the ink
        block came out the exact colour of the page behind it — 1.08 contrast against the card body,
        an anchor doing nothing. Measured, not guessed; the pane won't screenshot in this setup.

        In dark it takes the card surface and earns its edge from a rule instead, which is how a
        header separates from a body on a dark ground anyway. Light is untouched.
      */}
      <div className="bg-sidebar p-5 text-sidebar-foreground dark:border-b dark:border-border dark:bg-card">
        <div className="flex items-start gap-3">
          <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-primary font-display text-lg font-black text-primary-foreground">
            {initials(member.firstName, member.lastName)}
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="truncate font-display text-xl leading-tight font-black tracking-tight">
              {member.firstName} {member.lastName}
            </h2>
            <p className="mt-0.5 truncate text-sm text-sidebar-foreground/60">
              Member since {monthYearFormat.format(parseDateOnly(member.joinDate))} · {member.memberCode}
              {branchName ? ` · ${branchName}` : ''}
            </p>
          </div>
          {variant === 'rail' && onClose && (
            <button
              type="button"
              onClick={onClose}
              aria-label="Close member detail"
              className="-mt-1 -mr-1 flex size-8 shrink-0 items-center justify-center rounded-xl text-sidebar-foreground/60 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground"
            >
              <X className="size-4" />
            </button>
          )}
        </div>

        <div className="mt-4 flex flex-wrap items-center gap-2">
          <MemberStatusPill status={member.status} surface="ink" />
          {openVisitToday ? (
            // Already inside: offering "Check in" here would silently write a second visit, since
            // the endpoint has no duplicate guard. State the fact instead of the action.
            <span className="inline-flex items-center gap-1.5 rounded-xl bg-sidebar-accent px-2.5 py-1 text-xs font-medium text-sidebar-accent-foreground">
              <span className="size-1.5 rounded-full bg-primary" />
              In the gym since {timeFormat.format(openVisitToday)}
            </span>
          ) : (
            canCheckIn && (
              <Button
                className="h-9 rounded-xl px-4 font-display font-bold"
                onClick={handleCheckIn}
                disabled={checkIn.isPending}
              >
                {checkIn.isPending ? <Loader2 className="size-4 animate-spin" /> : <DoorOpen className="size-4" />}
                Check in
              </Button>
            )
          )}
          {variant === 'rail' && (
            <Button
              asChild
              variant="ghost"
              className="h-9 rounded-xl border border-sidebar-border px-3 text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground"
            >
              <Link to={`/members/${member.id}`}>
                Full profile
                <ArrowUpRight className="size-4" />
              </Link>
            </Button>
          )}
        </div>
      </div>

      {/*
        The design's fourth action was "Message". There is no staff→member message channel in this
        product: /api/coaching/clients/{id}/messages is trainer-to-own-client only and every handler
        checks the trainer↔member pairing first, so a receptionist pressing it would get a 403. It is
        omitted rather than wired to something that only works for one role.
      */}
      <Tabs defaultValue="overview" onValueChange={(v) => v === 'history' && setTimelineOpened(true)}>
        <TabsList className="h-auto w-full justify-start gap-5 rounded-none border-b border-border bg-transparent px-5 py-0">
          {['Overview', 'Billing', 'Training', 'Notes', 'History'].map((label) => (
            <TabsTrigger
              key={label}
              value={label.toLowerCase()}
              className="flex-none rounded-none border-0 border-b-2 border-transparent px-0 py-3 font-medium text-muted-foreground shadow-none data-[state=active]:border-foreground data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none"
            >
              {label}
            </TabsTrigger>
          ))}
        </TabsList>

        <TabsContent value="overview" className="space-y-4 p-5">
          {/* Plan status. Days remaining and the bar are pure endDate arithmetic; the price line is
              the price actually recorded against this membership. */}
          {plan ? (
            <div className="rounded-2xl bg-background p-4">
              <div className="flex items-start justify-between gap-2">
                <Eyebrow>{plan.membershipPlanName}</Eyebrow>
                <MembershipBadge status={plan.status} />
              </div>
              <p className="mt-2 font-display text-3xl leading-none font-black tracking-tight tabular-nums">
                {daysRemaining !== null && daysRemaining >= 0 ? (
                  <>
                    {daysRemaining}
                    <span className="ml-1.5 font-sans text-sm font-medium text-muted-foreground">
                      day{daysRemaining === 1 ? '' : 's'} remaining
                    </span>
                  </>
                ) : (
                  <span className="text-xl text-warning">Ended {planEnd ? dayMonthFormat.format(planEnd) : ''}</span>
                )}
              </p>
              {planProgress !== null && daysRemaining !== null && daysRemaining >= 0 && (
                <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-border">
                  <div
                    className="h-full rounded-full bg-primary transition-[width] duration-700 ease-out"
                    style={{ width: `${planProgress * 100}%` }}
                  />
                </div>
              )}
              <p className="mt-3 text-sm text-muted-foreground tabular-nums">
                {money(plan.pricePaid, plan.currency)} ·{' '}
                {plan.autoRenew ? 'renews' : 'ends'} {planEnd ? dayMonthFormat.format(planEnd) : ''}
              </p>
            </div>
          ) : (
            <div className="rounded-2xl bg-background p-4">
              <Eyebrow>Membership</Eyebrow>
              <p className="mt-1.5 text-sm text-muted-foreground">No membership on file yet.</p>
            </div>
          )}

          {/*
            Two tiles, not the design's three. "Classes" has no source — there is no per-member
            bookings query anywhere in the API, only a per-session roster — and "LTV" has neither a
            field nor a definition in this product. Visits are real: the attendance endpoint filters
            by memberId and by date.
          */}
          {canSeeVisits && (
            <div className="grid grid-cols-2 gap-2">
              <StatTile label="Visits · 30d" value={anyStale(visits30dQuery) ? '—' : (visits30dQuery.data ?? '—')} />
              {/* "Never" is a claim about the member, so it needs a query that actually answered —
                  on failure this says nothing rather than telling the desk a regular has never been in. */}
              <StatTile
                label="Last visit"
                value={
                  <span className="text-base">
                    {lastVisitAt ? relativeDay(lastVisitAt) : visitsQuery.isSuccess ? 'Never' : '—'}
                  </span>
                }
              />
            </div>
          )}

          <div>
            <Eyebrow>Recent activity</Eyebrow>
            <ul className="mt-2.5 space-y-3">
              {activity.map((entry) => (
                <li key={entry.id} className="flex items-start gap-3">
                  {entry.icon}
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{entry.title}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {relativeDay(entry.at)}
                      {entry.detail ? ` · ${entry.detail}` : ''}
                    </p>
                  </div>
                </li>
              ))}
              {activity.length === 0 && !activityFeedFailed && (
                <li className="text-sm text-muted-foreground">Nothing recorded yet.</li>
              )}
              {/* The feed merges several sources, so one failed query doesn't blank it — but it must
                  not pass off the surviving entries as the whole story either. */}
              {activityFeedFailed && (
                <li className="flex items-center gap-2 text-sm text-warning">
                  <TriangleAlert className="size-3.5 shrink-0" />
                  Some activity couldn't be loaded — this list may be incomplete.
                </li>
              )}
            </ul>
          </div>

          <div className="space-y-2 border-t border-border pt-4">
            <Eyebrow>Contact</Eyebrow>
            <p className="flex items-center gap-2 text-sm">
              <Mail className="size-3.5 shrink-0 text-muted-foreground" />
              <span className="truncate">{member.email}</span>
            </p>
            {member.phone && (
              <p className="flex items-center gap-2 text-sm">
                <Phone className="size-3.5 shrink-0 text-muted-foreground" />
                {member.phone}
              </p>
            )}
            {member.address && (
              <p className="flex items-center gap-2 text-sm">
                <MapPin className="size-3.5 shrink-0 text-muted-foreground" />
                <span className="truncate">{member.address}</span>
              </p>
            )}
            {member.dateOfBirth && (
              <p className="flex items-center gap-2 text-sm">
                <Cake className="size-3.5 shrink-0 text-muted-foreground" />
                {dayMonthFormat.format(parseDateOnly(member.dateOfBirth))}
              </p>
            )}
            {/* The active pairing only — the DTO resolves null for a member who trains on their own,
                and "no coach" is normal rather than a gap, so the line simply isn't there. */}
            {member.assignedTrainerName && (
              <p className="flex items-center gap-2 text-sm">
                <Dumbbell className="size-3.5 shrink-0 text-muted-foreground" />
                Coached by {member.assignedTrainerName}
              </p>
            )}
            {member.referredByName && (
              <p className="flex items-center gap-2 text-sm">
                <UserPlus className="size-3.5 shrink-0 text-muted-foreground" />
                Referred by{' '}
                {member.referredByMemberId ? (
                  <Link to={`/members/${member.referredByMemberId}`} className="underline underline-offset-2">
                    {member.referredByName}
                  </Link>
                ) : (
                  member.referredByName
                )}
              </p>
            )}
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <QrCode className="size-3.5 shrink-0" />
              <span className="font-mono text-xs">{member.qrCodeToken.slice(0, 12)}…</span>
            </p>
            {(canUpdate || canManageMembership) && (
              <div className="flex flex-wrap gap-2 pt-1">
                {canUpdate && <EditMemberDialog member={member} />}
                {/* A transfer moves the membership between branches, so it is manage_membership
                    rather than update — see MembersController.Transfer. */}
                {canManageMembership && (
                  <TransferMemberDialog memberId={member.id} currentBranchId={member.branchId} />
                )}
              </div>
            )}
          </div>
        </TabsContent>

        <TabsContent value="billing" className="space-y-4 p-5">
          <div className="flex items-center justify-between gap-2">
            <Eyebrow>Memberships</Eyebrow>
            {canManageMembership && <RenewMembershipDialog memberId={member.id} />}
          </div>
          {member.memberMemberships.length === 0 && <p className="text-sm text-muted-foreground">No membership history yet.</p>}
          <ul className="space-y-2">
            {member.memberMemberships.map((mm) => (
              <li key={mm.id} className="rounded-2xl border border-border p-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{mm.membershipPlanName}</p>
                    <p className="text-xs text-muted-foreground tabular-nums">
                      {dayMonthFormat.format(parseDateOnly(mm.startDate))} → {dayMonthFormat.format(parseDateOnly(mm.endDate))}
                    </p>
                    {mm.status === 'Cancelled' && mm.cancellationReason && (
                      <p className="mt-1 text-xs text-muted-foreground">Reason: {mm.cancellationReason}</p>
                    )}
                  </div>
                  <div className="shrink-0 text-right">
                    <MembershipBadge status={mm.status} />
                    <p className="mt-1 text-sm text-muted-foreground tabular-nums">{money(mm.pricePaid, mm.currency)}</p>
                  </div>
                </div>
                {/* The row only exists if it will hold something — without the guard, a role with
                    neither the invoice link nor the membership actions got a bare 8px gap. */}
                {(mm.invoiceId || canManageMembership) && (
                  <div className="mt-2 flex flex-wrap items-center gap-1">
                    {mm.invoiceId && (
                      <Link to={`/billing/${mm.invoiceId}`} className="px-2 text-sm text-primary hover:underline">
                        Invoice
                      </Link>
                    )}
                    {canManageMembership && mm.status === 'Active' && (
                      <FreezeMembershipDialog memberId={member.id} memberMembershipId={mm.id} />
                    )}
                    {canManageMembership && mm.status === 'Frozen' && (
                      <ResumeMembershipButton memberId={member.id} memberMembershipId={mm.id} />
                    )}
                    {canManageMembership &&
                      (mm.status === 'Active' || mm.status === 'Frozen' || mm.status === 'PendingActivation') && (
                        <CancelMembershipDialog memberId={member.id} memberMembershipId={mm.id} />
                      )}
                    {canManageMembership && mm.status === 'Cancelled' && (
                      <ReactivateMembershipButton memberId={member.id} memberMembershipId={mm.id} />
                    )}
                  </div>
                )}
              </li>
            ))}
          </ul>

          {/* Invoices are a separate module's data; a user without billing.view never asks for them. */}
          {canSeeBilling && (
            <div className="border-t border-border pt-4">
              <Eyebrow>Invoices</Eyebrow>
              {invoicesQuery.data?.items.length === 0 && (
                <p className="mt-2 text-sm text-muted-foreground">No invoices for this member.</p>
              )}
              <ul className="mt-2 space-y-1">
                {invoicesQuery.data?.items.map((invoice) => (
                  <li key={invoice.id}>
                    <Link
                      to={`/billing/${invoice.id}`}
                      className="flex items-center justify-between gap-2 rounded-xl px-2 py-2 transition-colors hover:bg-accent"
                    >
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-medium">{invoice.invoiceNumber}</span>
                        <span className="block text-xs text-muted-foreground tabular-nums">
                          {dayMonthFormat.format(parseDateOnly(invoice.issueDate))} · {invoice.status}
                        </span>
                      </span>
                      <span className="shrink-0 text-sm tabular-nums">{money(invoice.totalAmount, invoice.currency)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </TabsContent>

        <TabsContent value="training" className="space-y-4 p-5">
          {/*
            The tab used to open on measurements and photos — record-keeping — while the member's
            actual training lived only in the Workouts module. Sessions come first now: they are what
            "Training" means to the person being asked about it. Gated on workouts.view, the
            endpoint's own permission, so a receptionist keeps the tab without a section that could
            only 403 (their view starts at Measurements below).
          */}
          {canSeeWorkouts && (
            <div className="space-y-2">
              <Eyebrow>Sessions</Eyebrow>
              {workoutsUntrustworthy ? (
                <p className="flex items-center gap-2 text-sm text-warning">
                  <TriangleAlert className="size-3.5 shrink-0" />
                  Couldn't load this member's sessions.
                </p>
              ) : workoutLogsQuery.isLoading ? (
                <Skeleton className="h-24 w-full rounded-2xl" />
              ) : (workoutLogsQuery.data ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground">No sessions logged yet.</p>
              ) : (
                <ul className="space-y-1.5">
                  {(workoutLogsQuery.data ?? []).slice(0, 6).map((log) => (
                    <li key={log.id} className="rounded-xl border border-border px-3 py-2 text-sm">
                      <div className="flex items-center justify-between gap-2">
                        <span className="truncate font-medium">{log.workoutTemplateName ?? log.character}</span>
                        <span className="shrink-0 text-xs text-muted-foreground">
                          {relativeDay(new Date(log.loggedAt))}
                        </span>
                      </div>
                      <p className="mt-0.5 truncate text-xs text-muted-foreground">
                        {log.entries.length} exercise{log.entries.length === 1 ? '' : 's'}
                        {log.entries
                          .slice(0, 3)
                          .map((e) => ` · ${e.exerciseName}`)
                          .join('')}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}

          <div className={cn('flex items-center justify-between gap-2', canSeeWorkouts && 'border-t border-border pt-4')}>
            <Eyebrow>Measurements</Eyebrow>
            {canUpdate && <AddMeasurementDialog memberId={member.id} />}
          </div>
          {member.measurements.length === 0 && <p className="text-sm text-muted-foreground">No measurements logged.</p>}
          <ul className="space-y-1">
            {member.measurements.map((m) => (
              <li key={m.id} className="flex flex-wrap items-center gap-x-4 rounded-xl border border-border px-3 py-2 text-sm tabular-nums">
                <span className="text-muted-foreground">{dayMonthFormat.format(parseDateOnly(m.measuredOn))}</span>
                {m.weightKg !== null && <span>{m.weightKg} kg</span>}
                {m.bodyFatPercentage !== null && <span>{m.bodyFatPercentage}% body fat</span>}
              </li>
            ))}
          </ul>

          <div className="flex items-center justify-between gap-2 border-t border-border pt-4">
            <Eyebrow>Progress photos</Eyebrow>
            {canUpdate && <AddProgressPhotoDialog memberId={member.id} />}
          </div>
          {member.progressPhotos.length === 0 ? (
            <p className="text-sm text-muted-foreground">No progress photos yet.</p>
          ) : (
            <div className="grid grid-cols-3 gap-2">
              {member.progressPhotos.map((p) => (
                <div key={p.id} className="overflow-hidden rounded-xl border border-border">
                  <img src={p.photoUrl} alt="Progress" className="aspect-[2/3] w-full object-cover" />
                  <p className="p-1.5 text-[11px] text-muted-foreground">{dayMonthFormat.format(new Date(p.takenAt))}</p>
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="notes" className="space-y-4 p-5">
          <div className="flex items-center justify-between gap-2">
            <Eyebrow>Medical notes</Eyebrow>
            {canUpdate && <AddMedicalNoteDialog memberId={member.id} />}
          </div>
          {member.medicalNotes.length === 0 && <p className="text-sm text-muted-foreground">No medical notes on file.</p>}
          <ul className="space-y-2">
            {member.medicalNotes.map((n) => (
              <li key={n.id} className="rounded-2xl border border-border p-3 text-sm">
                <p>{n.note}</p>
                <p className="mt-1 text-xs text-muted-foreground">{relativeDay(new Date(n.recordedAt))}</p>
              </li>
            ))}
          </ul>

          <div className="flex items-center justify-between gap-2 border-t border-border pt-4">
            <Eyebrow>Emergency contacts</Eyebrow>
            {canUpdate && <AddEmergencyContactDialog memberId={member.id} />}
          </div>
          {member.emergencyContacts.length === 0 && (
            <p className="text-sm text-muted-foreground">No emergency contacts on file.</p>
          )}
          <ul className="space-y-2">
            {member.emergencyContacts.map((c) => (
              <li key={c.id} className="rounded-2xl border border-border p-3 text-sm">
                <p className="font-medium">
                  {c.name} <span className="font-normal text-muted-foreground">({c.relationship})</span>
                </p>
                <p className="text-muted-foreground">{c.phone}</p>
              </li>
            ))}
          </ul>
        </TabsContent>

        {/*
          One chronology instead of six modules to visit and reconcile from memory. What it contains
          is the server's decision, not this component's: each source is gated on the caller's own
          module permission, so a receptionist's history has no training in it and a trainer's has no
          money, and neither is told the other exists.
        */}
        <TabsContent value="history" className="space-y-3 p-5">
          {timelineUntrustworthy ? (
            <div className="flex flex-col items-center gap-3 py-8 text-center">
              <CloudOff className="size-7 text-muted-foreground" />
              <p className="text-sm font-medium">We couldn't load this member's history</p>
              <Button
                variant="outline"
                className="rounded-xl"
                onClick={() => timelineQuery.refetch()}
                disabled={timelineQuery.isFetching}
              >
                <RotateCw className={timelineQuery.isFetching ? 'size-4 animate-spin' : 'size-4'} />
                Try again
              </Button>
            </div>
          ) : timelineQuery.isLoading || !timelineQuery.data ? (
            <div className="space-y-2">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full rounded-2xl" />
              ))}
            </div>
          ) : timelineQuery.data.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nothing recorded for this member yet.</p>
          ) : (
            <ul className="space-y-3">
              {timelineQuery.data.map((entry, i) => (
                // Timeline entries carry no id of their own — they are assembled from six tables that
                // each have their own — so position within a stable, server-ordered list is the key.
                <TimelineRow key={`${entry.kind}-${entry.at}-${i}`} entry={entry} />
              ))}
            </ul>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}

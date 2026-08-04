import { Link } from 'react-router-dom'
import { CalendarDays, CalendarCheck, Dumbbell, Apple, QrCode, Droplets } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { StatCard } from '@/shared/components/StatCard'
import { useAuthStore } from '@/stores/authStore'
import {
  useMyAttendance,
  useMyClassBookings,
  useMyDietPlans,
  useMyProfile,
  useMyWaterLogs,
  useMyWorkoutAssignments,
  useMyWorkoutLogs,
} from '@/modules/portal/api/portalApi'

const classTimeFormat = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  hour: 'numeric',
  minute: '2-digit',
  timeZone: 'UTC',
})

function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <p className="font-medium">{title}</p>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

const dateFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' })
const dateTimeFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' })

export default function MemberPortalPage() {
  const user = useAuthStore((s) => s.user)
  const profile = useMyProfile()
  const attendance = useMyAttendance({ page: 1, pageSize: 10 })
  const workouts = useMyWorkoutLogs()
  const workoutAssignments = useMyWorkoutAssignments()
  const classBookings = useMyClassBookings()
  const dietPlans = useMyDietPlans()
  const waterLogs = useMyWaterLogs()

  if (profile.isError) {
    const status = (profile.error as { response?: { status?: number } })?.response?.status
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">Welcome, {user?.firstName}</h1>
        <p className="text-sm text-muted-foreground">
          {status === 404
            ? "Your account isn't linked to a member profile yet. Ask the front desk to link your login to your membership record."
            : 'Something went wrong loading your profile.'}
        </p>
      </div>
    )
  }

  const activeMembership = profile.data?.memberMemberships.find((m) => m.status === 'Active')
  const currentPlanLabel = activeMembership
    ? `${activeMembership.membershipPlanName} — active through ${dateFormat.format(new Date(activeMembership.endDate))}`
    : (profile.data?.memberMemberships[0]?.status ?? 'No membership on file')

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Welcome, {profile.data?.firstName ?? user?.firstName}
        </h1>
        <p className="text-sm text-muted-foreground">Your membership, attendance, and activity at Titan Fitness.</p>
      </div>

      {profile.isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <StatCard
            label="Membership Status"
            value={profile.data?.status ?? '—'}
            icon={CalendarDays}
            tone={profile.data?.status === 'Active' ? 'success' : 'warning'}
            hint={currentPlanLabel}
          />
          <StatCard label="Member Code" value={profile.data?.memberCode ?? '—'} icon={QrCode} />
          <StatCard
            label="Visits Logged"
            value={(attendance.data?.totalCount ?? 0).toLocaleString()}
            icon={CalendarDays}
          />
        </div>
      )}

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-2 space-y-0">
          <p className="font-medium">Your Upcoming Classes</p>
          <Button asChild size="sm" variant="outline">
            <Link to="/my-classes">Book a class</Link>
          </Button>
        </CardHeader>
        <CardContent>
          {classBookings.isLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : classBookings.data && classBookings.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {classBookings.data.map((b) => (
                <li key={b.bookingId} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <div className="flex min-w-0 items-center gap-2">
                    <span
                      className="size-2.5 shrink-0 rounded-full"
                      style={{ backgroundColor: b.colorHex ?? 'var(--muted-foreground)' }}
                    />
                    <span className="truncate font-medium">{b.classTypeName}</span>
                    <span className="shrink-0 text-muted-foreground">{classTimeFormat.format(new Date(b.startsAt))}</span>
                  </div>
                  {b.status === 'Waitlisted' && (
                    <Badge variant="secondary" className="shrink-0">
                      Waitlisted
                    </Badge>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <CalendarCheck className="size-6" />
              No classes booked yet — reserve your spot.
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <SectionCard title="Recent Check-ins">
          {attendance.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : attendance.data && attendance.data.items.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {attendance.data.items.map((a) => (
                <li key={a.id} className="flex items-center justify-between border-b pb-2 last:border-0">
                  <span>{dateTimeFormat.format(new Date(a.checkInAt))}</span>
                  <Badge variant="outline">{a.method === 'QrSimulated' ? 'QR' : 'Manual'}</Badge>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-muted-foreground">No check-ins yet.</p>
          )}
        </SectionCard>

        <SectionCard title="Workout Logs">
          {workouts.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : workouts.data && workouts.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {workouts.data.slice(0, 10).map((w) => (
                <li key={w.id} className="flex items-center justify-between border-b pb-2 last:border-0">
                  <span>{w.workoutTemplateName ?? 'Custom workout'}</span>
                  <span className="text-muted-foreground">{dateFormat.format(new Date(w.loggedAt))}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Dumbbell className="size-6" />
              No workouts logged yet.
            </div>
          )}
        </SectionCard>

        <SectionCard title="Assigned Workouts">
          {workoutAssignments.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : workoutAssignments.data && workoutAssignments.data.length > 0 ? (
            <ul className="space-y-3 text-sm">
              {workoutAssignments.data.map((a) => (
                <li key={a.id} className="space-y-1.5 border-b pb-3 last:border-0">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{a.workoutTemplateName}</span>
                    <span className="text-muted-foreground">
                      {dateFormat.format(new Date(a.startDate))}
                      {a.endDate ? ` → ${dateFormat.format(new Date(a.endDate))}` : ' → ongoing'}
                    </span>
                  </div>
                  {a.notes && <p className="text-muted-foreground">{a.notes}</p>}
                  <div className="flex flex-wrap gap-1">
                    {a.exercises.map((e) => (
                      <Badge key={e.id} variant="outline">
                        {e.exerciseName}: {e.setsCount}×{e.repsCount}
                      </Badge>
                    ))}
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Dumbbell className="size-6" />
              No workout plan assigned yet.
            </div>
          )}
        </SectionCard>

        <SectionCard title="Diet Plans">
          {dietPlans.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : dietPlans.data && dietPlans.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {dietPlans.data.map((p) => (
                <li key={p.id} className="flex items-center justify-between border-b pb-2 last:border-0">
                  <span>{p.name}</span>
                  <span className="text-muted-foreground">{p.targetCalories ? `${p.targetCalories} kcal/day` : '—'}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Apple className="size-6" />
              No diet plan assigned yet.
            </div>
          )}
        </SectionCard>

        <SectionCard title="Water Intake">
          {waterLogs.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : waterLogs.data && waterLogs.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {waterLogs.data.slice(0, 10).map((w) => (
                <li key={w.id} className="flex items-center justify-between border-b pb-2 last:border-0">
                  <span>{w.amountMl} ml</span>
                  <span className="text-muted-foreground">{dateTimeFormat.format(new Date(w.loggedAt))}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
              <Droplets className="size-6" />
              No water intake logged yet.
            </div>
          )}
        </SectionCard>
      </div>
    </div>
  )
}

import { AlertTriangle, TrendingUp } from 'lucide-react'

import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { useMemberVisits } from '@/modules/members/api/membersApi'
import { useMemberWorkoutAssignments } from '@/modules/workouts/api/workoutsApi'
import {
  useCoachingCompliance,
  useCoachingPlateaus,
  useCoachingRisks,
} from '@/modules/coaching/api/coachingApi'

const dayMonth = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short' })
const MS_PER_DAY = 86_400_000

function startOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate())
}

/** Recency, which is what a coach reads this for — the exact date only matters once it is old. */
function relativeDay(value: Date): string {
  const days = Math.round((startOfDay(new Date()).getTime() - startOfDay(value).getTime()) / MS_PER_DAY)
  if (days <= 0) return 'today'
  if (days === 1) return 'yesterday'
  if (days < 30) return `${days} days ago`
  return dayMonth.format(value)
}

/** A fact, or nothing. Never a placeholder standing in for a number we could not get. */
function Fact({ label, value }: { label: string; value: string | null }) {
  if (value === null) return null
  return (
    <span className="whitespace-nowrap">
      <span className="text-muted-foreground">{label} </span>
      <span className="font-medium tabular-nums">{value}</span>
    </span>
  )
}

/**
 * What is true about the client whose thread is open, sitting directly above it.
 *
 * The messaging half of the trainer's job was already here; this is the half that tells them what to
 * say. Every figure comes from an endpoint that already existed — attendance, workout assignments,
 * and the coaching compliance/risk/plateau queries that were being rendered on the Trainers screen,
 * aggregated across every trainer's clients, where the coach of any one of them would never look.
 * None of it is new data. It was filed by module rather than by who needs it mid-task.
 *
 * Those three queries are gym-wide by design (a manager reads them as a roster), so they are
 * filtered to this member here rather than refetched per client — one fetch serves every thread the
 * trainer opens.
 *
 * A missing figure renders as nothing at all. "Last visit —" next to a real adherence number reads
 * as a member who never comes in, and a coach would act on that.
 */
export function ClientContextBar({ memberId }: { memberId: string }) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const canSeeVisits = hasPermission('attendance.view')
  const canSeeWorkouts = hasPermission('workouts.view')

  const visits = useMemberVisits(memberId, canSeeVisits)
  const assignments = useMemberWorkoutAssignments(canSeeWorkouts ? memberId : undefined)
  const compliance = useCoachingCompliance()
  const risks = useCoachingRisks()
  const plateaus = useCoachingPlateaus()

  const mine = compliance.data?.find((r) => r.memberId === memberId)
  const myRisks = (risks.data ?? []).filter((r) => r.memberId === memberId)
  const myPlateaus = (plateaus.data ?? []).filter((r) => r.memberId === memberId)

  const lastVisitAt = visits.data?.items[0]?.checkInAt
  const lastWorkoutAt = mine?.lastWorkoutAt

  /*
   * The plan running today, not merely the newest row: an assignment that ended last month is not
   * what this member is on, and naming it would send the coach to the wrong programme. Compared as
   * date-only strings because that is what the API sends.
   */
  const today = new Date().toISOString().slice(0, 10)
  const activePlan = (assignments.data ?? [])
    .filter((a) => a.startDate.slice(0, 10) <= today && (!a.endDate || a.endDate.slice(0, 10) >= today))
    .sort((a, b) => b.startDate.localeCompare(a.startDate))[0]

  const facts = [
    canSeeVisits && lastVisitAt ? { label: 'Last visit', value: relativeDay(new Date(lastVisitAt)) } : null,
    lastWorkoutAt ? { label: 'Last session', value: relativeDay(new Date(lastWorkoutAt)) } : null,
    mine ? { label: 'Adherence', value: `${Math.round(mine.workoutAdherencePercent)}%` } : null,
    canSeeWorkouts && activePlan ? { label: 'Plan', value: activePlan.workoutTemplateName } : null,
  ].filter(Boolean) as { label: string; value: string }[]

  const flags = [
    ...myRisks.map((r) => ({ key: `risk-${r.riskType}`, tone: 'warning' as const, text: r.reason })),
    ...myPlateaus.map((p) => ({
      key: `plateau-${p.exerciseId}`,
      tone: 'muted' as const,
      text:
        p.suggestedNextWeightKg !== null && p.lastWeightKg !== null
          ? `${p.exerciseName} stuck at ${p.lastWeightKg} kg — try ${p.suggestedNextWeightKg} kg`
          : `${p.exerciseName} has not moved`,
    })),
  ]

  // Nothing known yet and nothing flagged: say nothing rather than draw an empty strip. The header
  // above already carries the member's name, which is the one thing that must always be there.
  if (facts.length === 0 && flags.length === 0) return null

  return (
    <div className="mt-2 space-y-1.5">
      {facts.length > 0 && (
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
          {facts.map((f) => (
            <Fact key={f.label} label={f.label} value={f.value} />
          ))}
        </div>
      )}

      {flags.map((f) => (
        <p
          key={f.key}
          className={cn(
            'flex items-start gap-1.5 text-sm',
            f.tone === 'warning' ? 'text-warning' : 'text-muted-foreground',
          )}
        >
          {f.tone === 'warning' ? (
            <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
          ) : (
            <TrendingUp className="mt-0.5 size-3.5 shrink-0" />
          )}
          {f.text}
        </p>
      ))}
    </div>
  )
}

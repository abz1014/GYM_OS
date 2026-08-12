import { Link } from 'react-router-dom'
import { Apple, CalendarClock, Check, Loader2, NotebookPen, Quote } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { MemberEmptyState, MemberLoadError } from '@/modules/portal/components/portalShared'
import {
  useLogMyPlanAdherence,
  useMyNutritionPrescription,
  type MyGuidance,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

const dateFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', timeZone: 'UTC' })
const longDateFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })

/** A target, printed only where one was set. A plan may set calories and nothing else. */
function Target({ label, value, unit }: { label: string; value: number | null; unit: string }) {
  if (value === null) return null
  return (
    <div className="rounded-2xl border border-border bg-card p-3 text-center">
      <p className="font-display text-[22px] leading-none font-black tabular-nums">{Math.round(value)}</p>
      <p className="mt-1 text-[11px] font-semibold text-muted-foreground uppercase">
        {label} · {unit}
      </p>
    </div>
  )
}

function GuidanceCard({ guidance, tone }: { guidance: MyGuidance; tone: 'week' | 'month' }) {
  return (
    <Card className={cn('rounded-3xl edge-light', tone === 'week' && 'border-primary/40 bg-accent')}>
      <CardContent className="py-4">
        <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
          {tone === 'week' ? 'This week' : 'This month'} · from {dateFmt.format(new Date(guidance.effectiveFrom))}
        </p>
        <p className="mt-2 font-display text-lg leading-tight font-black tracking-tight">{guidance.title}</p>
        {guidance.body && <p className="mt-2 text-sm leading-relaxed text-body-copy">{guidance.body}</p>}
      </CardContent>
    </Card>
  )
}

/**
 * The plan, as something you were given.
 *
 * This screen used to be four macro bars, a water card, and three buttons that all navigated to the
 * logging form. Its "consumed" halves came from logged meals — so for the overwhelming majority of
 * members, who do not keep a food diary, it was a permanent "0 / 2400 kcal" with an empty bar
 * against a real target. That does not read as "not tracked", it reads as total failure, every day,
 * on the screen whose job was to help.
 *
 * A nutrition plan is a prescription. So this shows the prescription: the targets, the
 * nutritionist's standing instructions, what changes this week and this month, when the plan is
 * next reviewed, and the history of what has changed — which is what makes it feel like a plan
 * progressing rather than a page sitting there.
 *
 * It asks for exactly one thing: a tap to say you stayed on it. That is not logging reintroduced by
 * the back door — it is the smallest honest instrument for the number the nutritionist's dashboard
 * is actually named after, and without it both the 15 XP a day and every trainer's compliance
 * reading would have silently gone to zero. See LogMyPlanAdherenceCommand.
 *
 * Food and water logging still exist for members who want them, on /log-activity, and still award
 * the same XP through the same per-day key. They are simply no longer this screen's job.
 */
export default function MyNutritionPage() {
  const prescription = useMyNutritionPrescription()
  const confirm = useLogMyPlanAdherence()

  const p = prescription.data
  const hasPlan = !!p?.planName

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <div>
        <h1 className="font-display text-3xl font-black tracking-tight">Nutrition</h1>
        <p className="text-sm text-muted-foreground">What your nutritionist has you on.</p>
      </div>

      {prescription.isLoading && <Skeleton className="h-64 w-full rounded-3xl shimmer" />}

      {isStale(prescription) && (
        <MemberLoadError
          title="We couldn't load your plan"
          hint="Your plan is still there — we just can't reach it right now."
          onRetry={() => void prescription.refetch()}
          isRetrying={prescription.isFetching}
        />
      )}

      {p && !hasPlan && (
        <MemberEmptyState
          icon={Apple}
          title="No plan yet"
          hint="Once a nutritionist writes you one, it appears here — targets, and what to change week to week."
        />
      )}

      {p && hasPlan && (
        <>
          <Card className="rounded-3xl edge-light">
            <CardContent className="py-5">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Your plan</p>
              <h2 className="mt-1 font-display text-2xl font-black tracking-tight">{p.planName}</h2>
              {p.authoredBy && (
                <p className="mt-1 text-sm text-muted-foreground">Written by {p.authoredBy}</p>
              )}

              {/* Only where a target was actually set. A plan with calories and no macros prints one
                  tile, not one tile and three dashes. */}
              <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
                <Target label="Calories" value={p.targetCalories} unit="kcal" />
                <Target label="Protein" value={p.targetProteinG} unit="g" />
                <Target label="Carbs" value={p.targetCarbsG} unit="g" />
                <Target label="Fat" value={p.targetFatG} unit="g" />
              </div>

              {p.notes && (
                <div className="mt-4 flex gap-2.5 rounded-2xl bg-muted p-3">
                  <Quote className="size-4 shrink-0 text-muted-foreground" />
                  <p className="text-sm leading-relaxed text-body-copy">{p.notes}</p>
                </div>
              )}

              {p.endDate && (
                <p className="mt-3 flex items-center gap-2 text-sm text-muted-foreground">
                  <CalendarClock className="size-4 shrink-0" />
                  Reviewed on {longDateFmt.format(new Date(p.endDate))}
                </p>
              )}
            </CardContent>
          </Card>

          {p.thisWeek && <GuidanceCard guidance={p.thisWeek} tone="week" />}
          {p.thisMonth && <GuidanceCard guidance={p.thisMonth} tone="month" />}

          {/* The one thing the screen asks for. Once confirmed it acknowledges rather than offering
              a button again — the server would return the existing row and the UI would report a
              success for a record it did not create. */}
          <Card
            className={cn(
              'rounded-3xl edge-light',
              p.confirmedToday && 'border-success/40 bg-success/[0.06]',
            )}
          >
            <CardContent className="py-5">
              {p.confirmedToday ? (
                <p className="flex items-center justify-center gap-2 font-display font-bold text-success">
                  <Check className="size-5" />
                  On plan today
                </p>
              ) : (
                <Button
                  className="press h-12 w-full rounded-2xl font-bold shadow-volt"
                  disabled={confirm.isPending}
                  onClick={() =>
                    confirm.mutate(null, {
                      onSuccess: () => toast.success('Nice — logged for today.'),
                      onError: () => toast.error("Couldn't record that."),
                    })
                  }
                >
                  {confirm.isPending && <Loader2 className="size-4 animate-spin" />}
                  I stayed on plan today
                </Button>
              )}

              <p className="mt-3 text-center text-sm text-muted-foreground tabular-nums">
                {p.daysConfirmedInWindow} of the last {p.adherenceWindowDays} days
              </p>
            </CardContent>
          </Card>

          {p.history.length > 0 && (
            <Card className="rounded-3xl edge-light">
              <CardContent className="py-4">
                <p className="mb-3 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                  What has changed
                </p>
                <ul className="space-y-3">
                  {p.history.map((g) => (
                    <li key={`${g.cadence}-${g.effectiveFrom}`} className="flex items-start justify-between gap-3">
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-semibold">{g.title}</span>
                        {g.body && <span className="block text-xs text-muted-foreground">{g.body}</span>}
                      </span>
                      <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                        {dateFmt.format(new Date(g.effectiveFrom))}
                      </span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}

          {/* Kept, quietly. Members who want a food diary still have one; it is simply not what this
              screen is for any more. */}
          <Button asChild variant="ghost" className="press h-11 w-full rounded-2xl text-muted-foreground">
            <Link to="/log-activity">
              <NotebookPen className="size-4" />
              Log a meal or water
            </Link>
          </Button>
        </>
      )}
    </div>
  )
}

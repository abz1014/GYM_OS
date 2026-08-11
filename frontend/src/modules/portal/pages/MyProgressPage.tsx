import { useState } from 'react'
import { Award, Camera, Check, CheckCircle2, Dumbbell, Loader2, Plus, Ruler, Trophy, TrendingDown, TrendingUp } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { TrendChart, type TrendPoint } from '@/shared/components/TrendChart'
import { CountUp } from '@/shared/components/uplift'
import { MasteryBar, MemberLoadError } from '@/modules/portal/components/portalShared'
import {
  useAchieveMyGoal,
  useCreateMyGoal,
  useMyMastery,
  useMyMeasurements,
  useMyProgress,
  useMyTimeline,
  useMyTrainingVolume,
  type GroupMastery,
  type MyGoal,
  type TimelineEntryType,
} from '@/modules/portal/api/portalApi'

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
const fullDateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })

const VOLUME_DAYS = 30

/**
 * Three questions a member actually asks of "progress", separated because they are answered by
 * different data and rarely asked at once: am I lifting more, is my body changing, am I turning up.
 * Previously all three were one long scroll, so the chart someone opened the page for sat below two
 * they didn't.
 */
const TABS = [
  { key: 'strength', label: 'Strength' },
  { key: 'body', label: 'Body' },
  { key: 'habits', label: 'Habits' },
] as const

type TabKey = (typeof TABS)[number]['key']

// Timeline entry type -> icon + accent colour.
const TIMELINE_STYLE: Record<TimelineEntryType, { icon: typeof Ruler; text: string }> = {
  Workout: { icon: Dumbbell, text: 'text-primary' },
  Measurement: { icon: Ruler, text: 'text-chart-2' },
  Photo: { icon: Camera, text: 'text-muted-foreground' },
  GoalAchieved: { icon: CheckCircle2, text: 'text-success' },
  PersonalRecord: { icon: Trophy, text: 'text-warning' },
  Achievement: { icon: Award, text: 'text-primary' },
}

function GoalRow({ goal }: { goal: MyGoal }) {
  const achieve = useAchieveMyGoal()

  const handleAchieve = () =>
    achieve.mutate(goal.id, {
      onSuccess: () => toast.success('Goal achieved — nice work!'),
      onError: () => toast.error('Could not update the goal.'),
    })

  return (
    <div className="flex items-center justify-between gap-3 py-2">
      <div className="min-w-0">
        <p className={`truncate text-sm font-medium ${goal.isAchieved ? 'text-muted-foreground line-through' : ''}`}>
          {goal.title}
        </p>
        <p className="text-xs text-muted-foreground">
          {goal.isAchieved && goal.achievedAt
            ? `Achieved ${fullDateFmt.format(new Date(goal.achievedAt))}`
            : goal.targetDate
              ? `Target ${fullDateFmt.format(new Date(`${goal.targetDate}T00:00:00Z`))}`
              : 'No target date'}
        </p>
      </div>
      {goal.isAchieved ? (
        <Badge variant="secondary" className="shrink-0 gap-1">
          <Check className="size-3" />
          Done
        </Badge>
      ) : (
        <Button size="sm" variant="outline" className="shrink-0 rounded-xl" disabled={achieve.isPending} onClick={handleAchieve}>
          {achieve.isPending && <Loader2 className="size-4 animate-spin" />}
          Mark achieved
        </Button>
      )}
    </div>
  )
}

function AddGoalDialog() {
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [targetDate, setTargetDate] = useState('')
  const create = useCreateMyGoal()

  const handleSubmit = () => {
    if (!title.trim()) return
    create.mutate(
      { title: title.trim(), targetDate: targetDate || null },
      {
        onSuccess: () => {
          toast.success('Goal added.')
          setTitle('')
          setTargetDate('')
          setOpen(false)
        },
        onError: () => toast.error('Could not add the goal.'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" className="gap-1 rounded-xl">
          <Plus className="size-4" />
          Add goal
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Set a goal</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="goal-title">What do you want to achieve?</Label>
            <Input
              id="goal-title"
              placeholder="e.g. Run a 5k without stopping"
              maxLength={200}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="goal-date">Target date (optional)</Label>
            <Input id="goal-date" type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button disabled={!title.trim() || create.isPending} onClick={handleSubmit}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Add goal
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** A small "up/down x%" chip. Returns nothing at all when there is no prior period to compare to. */
function DeltaChip({ percent }: { percent: number | null }) {
  if (percent === null) return null
  const up = percent >= 0
  return (
    <span
      className={cn(
        'flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-bold tabular-nums',
        up ? 'bg-success/15 text-success' : 'bg-warning/15 text-warning',
      )}
    >
      {up ? <TrendingUp className="size-3" /> : <TrendingDown className="size-3" />}
      {up ? '+' : ''}
      {percent}%
    </span>
  )
}

function StrengthTab() {
  const volumeQuery = useMyTrainingVolume(VOLUME_DAYS)
  const masteryQuery = useMyMastery()
  const volume = volumeQuery.data

  /*
   * This tab owns its own query, so the page-level isError branch above cannot cover it — on a
   * failed volume call the page rendered its error line AND this card underneath, counting animatedly
   * up to "0kg" over "Total volume · 30 days" with "Log a workout to start your volume trend" beneath
   * it. Three separate statements that the member has not trained in a month, one of them animated
   * for emphasis. The card is replaced outright rather than zeroed.
   */
  if (volumeQuery.isError) {
    return (
      <Card className="rounded-3xl edge-light">
        <CardContent className="py-5">
          <MemberLoadError
            title="We couldn't load your volume"
            hint="Every session you've logged is still recorded."
            onRetry={() => void volumeQuery.refetch()}
            isRetrying={volumeQuery.isFetching}
          />
        </CardContent>
      </Card>
    )
  }

  const points: TrendPoint[] = (volume ?? []).map((v) => ({
    label: dateFmt.format(new Date(`${v.date}T00:00:00Z`)),
    value: Number(v.volumeKg),
  }))

  const total = points.reduce((sum, p) => sum + p.value, 0)
  const trainingDays = points.filter((p) => p.value > 0).length

  /**
   * The second half of the window against the first. A percentage needs a previous period, and this
   * series is the only one the member has — so the comparison is stated as what it actually is
   * rather than borrowed from a longer history the endpoint never returned. No prior half with any
   * volume in it means no chip at all, rather than a division by zero dressed up as "+100%".
   */
  const half = Math.floor(points.length / 2)
  const earlier = points.slice(0, half).reduce((sum, p) => sum + p.value, 0)
  const later = points.slice(half).reduce((sum, p) => sum + p.value, 0)
  const deltaPercent = half > 0 && earlier > 0 ? Math.round(((later - earlier) / earlier) * 100) : null

  return (
    <div className="space-y-4">
      <Card className="rounded-3xl edge-light">
        <CardContent className="py-5">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            Total volume · {VOLUME_DAYS} days
          </p>
          <div className="mt-1 flex items-center gap-3">
            {/*
              One formatted string, not a number with a unit hung off the side: "34.8t" and "940kg"
              are different units, and a roll-up that reformatted mid-count would flick between them.
              CountUp keeps the caller's own formatter for exactly that reason.
            */}
            <CountUp
              to={total}
              format={(v) => (v >= 1000 ? `${(v / 1000).toFixed(1)}t` : `${Math.round(v)}kg`)}
              className="font-display text-4xl leading-none font-black tracking-tight tabular-nums"
            />
            <DeltaChip percent={deltaPercent} />
          </div>
          {deltaPercent !== null && (
            <p className="mt-1 text-xs text-muted-foreground">
              Last {points.length - half} days vs the {half} before.
            </p>
          )}

          {/*
            A line/area chart, never bars. Progress over time is a shape, and the whole point of
            `zeroBaseline` here is that rest days sit on the floor so the shape carries the direction.
            `glow` is the uplift's one behavioural addition: a persistent marker on the last real
            session, which a 30-point series otherwise only shows on hover.
          */}
          <div className="mt-4">
            <TrendChart
              data={points}
              colorClassName="text-primary"
              zeroBaseline
              glow
              valueFormatter={(v) => (v >= 1000 ? `${(v / 1000).toFixed(1)}t` : `${Math.round(v)}kg`)}
              emptyMessage="Log a workout to start your volume trend."
            />
          </div>
        </CardContent>
      </Card>

      {points.length > 0 && (
        <div className="grid grid-cols-2 gap-3">
          <Card className="rounded-2xl edge-light">
            <CardContent className="py-4">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Total lifted</p>
              <p className="mt-1 font-display text-2xl font-black tracking-tight tabular-nums">
                {Math.round(total).toLocaleString()}
                <span className="ml-1 text-sm font-bold text-muted-foreground">kg</span>
              </p>
            </CardContent>
          </Card>
          <Card className="rounded-2xl edge-light">
            <CardContent className="py-4">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Training days</p>
              <p className="mt-1 font-display text-2xl font-black tracking-tight tabular-nums">
                {trainingDays}
                <span className="ml-1 text-sm font-bold text-muted-foreground">/ {VOLUME_DAYS}</span>
              </p>
            </CardContent>
          </Card>
        </div>
      )}

      {/*
        Mastery belongs on Strength, not Habits, and not on My Training where it used to live.
        "How much of my training has gone into each area" is a strength question; a member looking
        for it will look here, beside volume and personal records. On My Training its thirteen bars
        were the single largest contributor to a screen a real member called an information bomb.

        Own query rather than a prop, like the volume query above — this tab already owns its data.
      */}
      {masteryQuery.data && masteryQuery.data.exercises.length > 0 && (
        <Card className="rounded-3xl edge-light">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 font-display text-base font-bold">
              <Dumbbell className="size-4" />
              Mastery
            </CardTitle>
            <p className="text-sm text-muted-foreground">
              How much of your logged training each area accounts for.
            </p>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Muscle groups</p>
              {masteryQuery.data.muscleGroups.map((g: GroupMastery) => (
                <MasteryBar key={g.name} label={g.name} percent={g.masteryPercent} />
              ))}
            </div>
            <div className="space-y-2">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Top exercises</p>
              {masteryQuery.data.exercises.slice(0, 5).map((e) => (
                <MasteryBar
                  key={e.exerciseId}
                  label={e.exerciseName}
                  percent={e.masteryPercent}
                  hint={`${e.sessions} session${e.sessions === 1 ? '' : 's'}`}
                />
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function BodyTab({ photos }: { photos: { id: string; photoUrl: string; takenAt: string }[] }) {
  const measurementsQuery = useMyMeasurements()
  const measurements = measurementsQuery.data

  /*
   * Same shape as StrengthTab, and the same reason. The body-weight figure already falls back to an
   * em dash rather than a zero, but "Log a measurement to start your weight trend." underneath it is
   * still a claim that the member has never been weighed — and the photo strip below reads as an
   * empty history rather than an unreachable one.
   */
  if (measurementsQuery.isError) {
    return (
      <Card className="rounded-3xl edge-light">
        <CardContent className="py-5">
          <MemberLoadError
            title="We couldn't load your measurements"
            hint="Nothing you've recorded has been lost."
            onRetry={() => void measurementsQuery.refetch()}
            isRetrying={measurementsQuery.isFetching}
          />
        </CardContent>
      </Card>
    )
  }

  const weightPoints: TrendPoint[] = (measurements ?? [])
    .filter((m) => m.weightKg !== null)
    .map((m) => ({ label: dateFmt.format(new Date(`${m.measuredOn}T00:00:00Z`)), value: Number(m.weightKg) }))

  const bodyFatPoints: TrendPoint[] = (measurements ?? [])
    .filter((m) => m.bodyFatPercentage !== null)
    .map((m) => ({ label: dateFmt.format(new Date(`${m.measuredOn}T00:00:00Z`)), value: Number(m.bodyFatPercentage) }))

  const latestWeight = weightPoints.at(-1)?.value ?? null
  const weightDelta =
    weightPoints.length >= 2 ? weightPoints[weightPoints.length - 1].value - weightPoints[0].value : null

  return (
    <div className="space-y-4">
      <Card className="rounded-3xl edge-light">
        <CardContent className="py-5">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Body weight</p>
          <div className="mt-1 flex items-center gap-3">
            <span className="font-display text-4xl leading-none font-black tracking-tight tabular-nums">
              {latestWeight === null ? '—' : latestWeight.toFixed(1)}
              {latestWeight !== null && <span className="ml-1 text-lg font-bold text-muted-foreground">kg</span>}
            </span>
            {weightDelta !== null && (
              <span
                className={cn(
                  'flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-bold tabular-nums',
                  // Neither direction is "good" without knowing the member's goal, so both read
                  // neutral rather than green-for-down.
                  'bg-muted text-muted-foreground',
                )}
              >
                {weightDelta <= 0 ? <TrendingDown className="size-3" /> : <TrendingUp className="size-3" />}
                {weightDelta > 0 ? '+' : ''}
                {weightDelta.toFixed(1)} kg
              </span>
            )}
          </div>
          <div className="mt-4">
            <TrendChart
              data={weightPoints}
              colorClassName="text-chart-2"
              valueFormatter={(v) => `${v.toFixed(1)} kg`}
              emptyMessage="Log a measurement to start your weight trend."
            />
          </div>
        </CardContent>
      </Card>

      {bodyFatPoints.length > 0 && (
        <Card className="rounded-3xl edge-light">
          <CardContent className="py-5">
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Body fat</p>
            <div className="mt-4">
              <TrendChart data={bodyFatPoints} colorClassName="text-chart-2" valueFormatter={(v) => `${v.toFixed(1)}%`} />
            </div>
          </CardContent>
        </Card>
      )}

      {photos.length > 0 && (
        <Card className="rounded-3xl edge-light">
          <CardContent className="py-5">
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Progress photos</p>
            <div className="mt-3 flex gap-3 overflow-x-auto pb-1">
              {photos.map((p) => (
                <figure key={p.id} className="shrink-0">
                  <img
                    src={p.photoUrl}
                    alt={`Progress photo from ${fullDateFmt.format(new Date(p.takenAt))}`}
                    className="h-40 w-28 rounded-2xl object-cover"
                    loading="lazy"
                  />
                  <figcaption className="mt-1 text-center text-[10px] text-muted-foreground">
                    {dateFmt.format(new Date(p.takenAt))}
                  </figcaption>
                </figure>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

export default function MyProgressPage() {
  const { data: progress, isLoading, isError } = useMyProgress()
  const { data: timeline } = useMyTimeline()
  const [tab, setTab] = useState<TabKey>('strength')

  const openGoals = progress?.goals.filter((g) => !g.isAchieved) ?? []
  const achievedGoals = progress?.goals.filter((g) => g.isAchieved) ?? []

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <h1 className="font-display text-3xl font-black tracking-tight">Progress</h1>

      <div className="flex gap-1 rounded-2xl bg-muted p-1">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => setTab(t.key)}
            aria-current={t.key === tab ? 'true' : undefined}
            className={cn(
              'h-11 flex-1 rounded-xl text-sm font-bold transition-[transform,background-color,color] duration-[120ms] ease-[var(--ease-uplift)] active:scale-[.97]',
              t.key === tab
                ? 'bg-primary text-primary-foreground shadow-volt'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isError && (
        <p className="text-sm text-muted-foreground">
          We couldn't load your progress. Ask the front desk if your membership is linked to your login.
        </p>
      )}

      {isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-56 w-full rounded-3xl shimmer" />
          <Skeleton className="h-24 w-full rounded-2xl shimmer" />
        </div>
      )}

      {tab === 'strength' && <StrengthTab />}
      {tab === 'body' && <BodyTab photos={progress?.photos ?? []} />}

      {tab === 'habits' && progress && (
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-2 sm:gap-3">
            {[
              { label: 'Week streak', value: progress.weeklyStreak },
              { label: 'This month', value: progress.visitsThisMonth },
              { label: 'Total visits', value: progress.totalVisits },
            ].map((stat) => (
              <Card key={stat.label} className="rounded-2xl edge-light">
                <CardContent className="px-3 py-4 text-center">
                  <p className="font-display text-3xl leading-none font-black tracking-tight tabular-nums">
                    {stat.value}
                  </p>
                  <p className="mt-1 text-[11px] font-bold tracking-wider text-muted-foreground uppercase">
                    {stat.label}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>

          <Card className="rounded-3xl edge-light">
            <CardHeader className="pb-2">
              <CardTitle className="font-display text-base font-bold">Goals</CardTitle>
              <CardAction>
                <AddGoalDialog />
              </CardAction>
            </CardHeader>
            <CardContent>
              {progress.goals.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">
                  No goals yet — set one and we'll keep it front and center.
                </p>
              ) : (
                <div className="divide-y divide-border">
                  {openGoals.map((g) => (
                    <GoalRow key={g.id} goal={g} />
                  ))}
                  {achievedGoals.map((g) => (
                    <GoalRow key={g.id} goal={g} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {timeline && timeline.length > 0 && (
            <Card className="rounded-3xl edge-light">
              <CardHeader className="pb-2">
                <CardTitle className="font-display text-base font-bold">Your story so far</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3">
                  {timeline.map((entry, index) => {
                    const style = TIMELINE_STYLE[entry.type]
                    const Icon = style.icon
                    return (
                      <li key={`${entry.type}-${entry.occurredAt}-${index}`} className="flex items-start gap-3">
                        <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-xl bg-muted">
                          <Icon className={`size-4 ${style.text}`} />
                        </span>
                        <div className="min-w-0">
                          <p className="text-sm font-medium">{entry.title}</p>
                          {entry.description && <p className="text-sm text-muted-foreground">{entry.description}</p>}
                          {entry.photoUrl && (
                            <img
                              src={entry.photoUrl}
                              alt={entry.title}
                              className="mt-1 h-24 w-16 rounded-xl object-cover"
                              loading="lazy"
                            />
                          )}
                          <p className="mt-0.5 text-[11px] text-muted-foreground">
                            {fullDateFmt.format(new Date(entry.occurredAt))}
                          </p>
                        </div>
                      </li>
                    )
                  })}
                </ul>
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  )
}

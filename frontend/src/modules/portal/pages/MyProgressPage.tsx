import { useState } from 'react'
import { CalendarCheck, Camera, Check, CheckCircle2, Dumbbell, Flame, History, Loader2, Plus, Ruler, Scale, Target, Trophy, Award, TrendingDown, TrendingUp } from 'lucide-react'
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
import { TrendChart, type TrendPoint } from '@/shared/components/TrendChart'
import {
  useAchieveMyGoal,
  useCreateMyGoal,
  useMyMeasurements,
  useMyProgress,
  useMyTimeline,
  useMyTrainingVolume,
  type MyGoal,
  type TimelineEntryType,
} from '@/modules/portal/api/portalApi'

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
const fullDateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })

// Timeline entry type -> icon + accent color.
const TIMELINE_STYLE: Record<TimelineEntryType, { icon: typeof Ruler; text: string }> = {
  Workout: { icon: Dumbbell, text: 'text-primary' },
  Measurement: { icon: Ruler, text: 'text-sky-600' },
  Photo: { icon: Camera, text: 'text-violet-600' },
  GoalAchieved: { icon: CheckCircle2, text: 'text-emerald-600' },
  PersonalRecord: { icon: Trophy, text: 'text-amber-600' },
  Achievement: { icon: Award, text: 'text-indigo-600' },
}

/**
 * Hand-rolled SVG line chart, matching the codebase's no-chart-dependency approach (see
 * SimpleBarChart). Y axis spans the data's own min/max with padding so a 3 kg change over 3
 * months reads as a visible slope instead of a flat line on a 0-based axis.
 */
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
        <Button size="sm" variant="outline" className="shrink-0" disabled={achieve.isPending} onClick={handleAchieve}>
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
        <Button size="sm" variant="outline" className="gap-1">
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


/**
 * The two series a member actually judges progress by. Both were previously invisible: body weight
 * existed only as a "first vs latest" delta with no shape, and training volume had no endpoint at
 * all — so neither trend nor plateau was ever legible.
 */
function ProgressCharts() {
  const { data: measurements } = useMyMeasurements()
  const { data: volume } = useMyTrainingVolume(30)

  const weightPoints: TrendPoint[] = (measurements ?? [])
    .filter((m) => m.weightKg !== null)
    .map((m) => ({ label: dateFmt.format(new Date(`${m.measuredOn}T00:00:00Z`)), value: Number(m.weightKg) }))

  const bodyFatPoints: TrendPoint[] = (measurements ?? [])
    .filter((m) => m.bodyFatPercentage !== null)
    .map((m) => ({ label: dateFmt.format(new Date(`${m.measuredOn}T00:00:00Z`)), value: Number(m.bodyFatPercentage) }))

  const volumePoints: TrendPoint[] = (volume ?? []).map((v) => ({
    label: dateFmt.format(new Date(`${v.date}T00:00:00Z`)),
    value: Number(v.volumeKg),
  }))

  const weightDelta =
    weightPoints.length >= 2 ? weightPoints[weightPoints.length - 1].value - weightPoints[0].value : null

  const totalVolume = volumePoints.reduce((sum, p) => sum + p.value, 0)
  const trainingDays = volumePoints.filter((p) => p.value > 0).length

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Scale className="size-4 text-sky-600" />
            Body weight
          </CardTitle>
          {weightDelta !== null && (
            <CardAction>
              <Badge variant="secondary" className="gap-1">
                {weightDelta <= 0 ? <TrendingDown className="size-3" /> : <TrendingUp className="size-3" />}
                {weightDelta > 0 ? '+' : ''}
                {weightDelta.toFixed(1)} kg
              </Badge>
            </CardAction>
          )}
        </CardHeader>
        <CardContent>
          <TrendChart
            data={weightPoints}
            colorClassName="text-sky-600"
            valueFormatter={(v) => `${v.toFixed(1)} kg`}
            emptyMessage="Log a measurement to start your weight trend."
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <TrendingUp className="size-4 text-primary" />
            Training volume · last 30 days
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <TrendChart
            data={volumePoints}
            colorClassName="text-primary"
            zeroBaseline
            valueFormatter={(v) => (v >= 1000 ? `${(v / 1000).toFixed(1)}t` : `${Math.round(v)}kg`)}
            emptyMessage="Log a workout to start your volume trend."
          />
          {volumePoints.length > 0 && (
            <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
              <span>
                Total lifted:{' '}
                <span className="font-medium text-foreground tabular-nums">{Math.round(totalVolume).toLocaleString()} kg</span>
              </span>
              <span>
                Training days: <span className="font-medium text-foreground tabular-nums">{trainingDays}/30</span>
              </span>
            </div>
          )}
        </CardContent>
      </Card>

      {bodyFatPoints.length > 0 && (
        <Card className="lg:col-span-2">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <TrendingDown className="size-4 text-violet-600" />
              Body fat
            </CardTitle>
          </CardHeader>
          <CardContent>
            <TrendChart
              data={bodyFatPoints}
              colorClassName="text-violet-600"
              valueFormatter={(v) => `${v.toFixed(1)}%`}
            />
          </CardContent>
        </Card>
      )}
    </div>
  )
}

export default function MyProgressPage() {
  const { data: progress, isLoading, isError } = useMyProgress()
  const { data: timeline } = useMyTimeline()


  const openGoals = progress?.goals.filter((g) => !g.isAchieved) ?? []
  const achievedGoals = progress?.goals.filter((g) => g.isAchieved) ?? []

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Progress</h1>
        <p className="text-sm text-muted-foreground">Your trends, streaks, records and milestones.</p>
      </div>

      <ProgressCharts />

      {isError && (
        <p className="text-sm text-muted-foreground">
          We couldn't load your progress. Ask the front desk if your membership is linked to your login.
        </p>
      )}

      {isLoading && (
        <div className="space-y-3">
          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full" />
            ))}
          </div>
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      )}

      {progress && (
        <>
          <div className="grid grid-cols-3 gap-2 sm:gap-3">
            <Card>
              <CardContent className="flex flex-col items-center gap-1 p-3 text-center sm:p-4">
                <Flame className={`size-5 ${progress.weeklyStreak > 0 ? 'text-orange-500' : 'text-muted-foreground'}`} />
                <span className="text-2xl font-semibold tabular-nums">{progress.weeklyStreak}</span>
                <span className="text-xs text-muted-foreground">week streak</span>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="flex flex-col items-center gap-1 p-3 text-center sm:p-4">
                <CalendarCheck className="size-5 text-muted-foreground" />
                <span className="text-2xl font-semibold tabular-nums">{progress.visitsThisMonth}</span>
                <span className="text-xs text-muted-foreground">visits this month</span>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="flex flex-col items-center gap-1 p-3 text-center sm:p-4">
                <Target className="size-5 text-muted-foreground" />
                <span className="text-2xl font-semibold tabular-nums">{progress.totalVisits}</span>
                <span className="text-xs text-muted-foreground">total visits</span>
              </CardContent>
            </Card>
          </div>


          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <Target className="size-4" />
                Goals
              </CardTitle>
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
                <div className="divide-y">
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

          {progress.photos.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Progress photos</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex gap-3 overflow-x-auto pb-1">
                  {progress.photos.map((p) => (
                    <figure key={p.id} className="shrink-0">
                      <img
                        src={p.photoUrl}
                        alt={`Progress photo from ${fullDateFmt.format(new Date(p.takenAt))}`}
                        className="h-40 w-28 rounded-md object-cover"
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

          {timeline && timeline.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base">
                  <History className="size-4" />
                  Transformation timeline
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3">
                  {timeline.map((entry, index) => {
                    const style = TIMELINE_STYLE[entry.type]
                    const Icon = style.icon
                    return (
                      <li key={`${entry.type}-${entry.occurredAt}-${index}`} className="flex items-start gap-3">
                        <Icon className={`mt-0.5 size-4 shrink-0 ${style.text}`} />
                        <div className="min-w-0">
                          <p className="text-sm font-medium">{entry.title}</p>
                          {entry.description && <p className="text-sm text-muted-foreground">{entry.description}</p>}
                          {entry.photoUrl && (
                            <img
                              src={entry.photoUrl}
                              alt={entry.title}
                              className="mt-1 h-24 w-16 rounded-md object-cover"
                              loading="lazy"
                            />
                          )}
                          <p className="mt-0.5 text-[11px] text-muted-foreground">{fullDateFmt.format(new Date(entry.occurredAt))}</p>
                        </div>
                      </li>
                    )
                  })}
                </ul>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  )
}

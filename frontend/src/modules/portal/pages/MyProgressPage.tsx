import { useState } from 'react'
import { CalendarCheck, Check, Flame, Loader2, Plus, Scale, Target, TrendingDown, TrendingUp } from 'lucide-react'
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
import {
  useAchieveMyGoal,
  useCreateMyGoal,
  useMyProgress,
  type MyGoal,
  type MyWeightPoint,
} from '@/modules/portal/api/portalApi'

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
const fullDateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })

/**
 * Hand-rolled SVG line chart, matching the codebase's no-chart-dependency approach (see
 * SimpleBarChart). Y axis spans the data's own min/max with padding so a 3 kg change over 3
 * months reads as a visible slope instead of a flat line on a 0-based axis.
 */
function WeightTrendChart({ points }: { points: MyWeightPoint[] }) {
  const W = 300
  const H = 90
  const PAD = 8

  const weights = points.map((p) => p.weightKg)
  const min = Math.min(...weights)
  const max = Math.max(...weights)
  const span = Math.max(max - min, 0.1)

  const x = (i: number) => (points.length === 1 ? W / 2 : PAD + (i * (W - 2 * PAD)) / (points.length - 1))
  const y = (w: number) => PAD + ((max - w) * (H - 2 * PAD)) / span

  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${x(i).toFixed(1)} ${y(p.weightKg).toFixed(1)}`).join(' ')

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="h-24 w-full" preserveAspectRatio="none" role="img" aria-label="Weight trend">
      <path d={path} fill="none" stroke="var(--primary)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      {points.map((p, i) => (
        <circle key={i} cx={x(i)} cy={y(p.weightKg)} r="3" fill="var(--primary)">
          <title>{`${dateFmt.format(new Date(`${p.measuredOn}T00:00:00Z`))}: ${p.weightKg} kg`}</title>
        </circle>
      ))}
    </svg>
  )
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

export default function MyProgressPage() {
  const { data: progress, isLoading, isError } = useMyProgress()

  const weightChange =
    progress && progress.weightTrend.length >= 2
      ? progress.weightTrend[progress.weightTrend.length - 1].weightKg - progress.weightTrend[0].weightKg
      : null

  const openGoals = progress?.goals.filter((g) => !g.isAchieved) ?? []
  const achievedGoals = progress?.goals.filter((g) => g.isAchieved) ?? []

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Progress</h1>
        <p className="text-sm text-muted-foreground">Your streak, visits, and goals — keep it going.</p>
      </div>

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
                <Scale className="size-4" />
                Weight trend
              </CardTitle>
              {weightChange !== null && (
                <CardAction>
                  <Badge variant="secondary" className="gap-1">
                    {weightChange <= 0 ? <TrendingDown className="size-3" /> : <TrendingUp className="size-3" />}
                    {weightChange > 0 ? '+' : ''}
                    {weightChange.toFixed(1)} kg
                  </Badge>
                </CardAction>
              )}
            </CardHeader>
            <CardContent>
              {progress.weightTrend.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">
                  No measurements yet — ask your trainer to log one at your next visit.
                </p>
              ) : (
                <>
                  <WeightTrendChart points={progress.weightTrend} />
                  <div className="mt-1 flex justify-between text-[10px] text-muted-foreground">
                    <span>{dateFmt.format(new Date(`${progress.weightTrend[0].measuredOn}T00:00:00Z`))}</span>
                    <span>
                      {dateFmt.format(
                        new Date(`${progress.weightTrend[progress.weightTrend.length - 1].measuredOn}T00:00:00Z`),
                      )}
                    </span>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

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
        </>
      )}
    </div>
  )
}

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarDays, ChevronRight, CloudOff, Dumbbell, Flame, Lightbulb, Pencil, RotateCw } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ActivityRing } from '@/shared/components/ActivityRing'
import { WeeklyGoalDialog } from '@/modules/portal/components/WeeklyGoalDialog'
import { useMyToday } from '@/modules/portal/api/portalApi'

const classTimeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })

function greeting(): string {
  const h = new Date().getHours()
  if (h < 12) return 'Good morning'
  if (h < 18) return 'Good afternoon'
  return 'Good evening'
}

/**
 * The member's home. Previously this route showed membership status and a member code — real
 * information, but back-office information, and it forced a member to scroll past their admin
 * details to reach anything they'd open the app for. That content now lives on More > Membership.
 *
 * What's left answers one question on sight — "am I on track this week?" — and offers the single
 * action a member is here to take. Everything else is one tap away.
 *
 * Every number here comes from /api/me/today rather than being stitched together from separate
 * calls: the week, the session count and the streak are one consistent answer computed once,
 * server-side, instead of five independently-cached responses the browser had to reconcile.
 */
export default function TodayPage() {
  const today = useMyToday()
  const [editingGoal, setEditingGoal] = useState(false)

  const data = today.data
  const goal = data?.weeklySessionGoal ?? 0
  const sessions = data?.sessionsThisWeek ?? 0
  const remaining = data?.remainingSessions ?? 0
  const goalMet = data?.goalMet ?? false
  const streakWeeks = data?.workoutStreakWeeks ?? 0

  /**
   * When the request fails outright there is nothing honest to draw. Falling through to the normal
   * layout would render a closed-nothing ring and a zero streak — which reads as "you have trained
   * nothing this week and your streak is gone" rather than "we couldn't check". For a screen whose
   * whole motivational weight rests on a streak, inventing a zero is the worst thing it could say,
   * so the page says it doesn't know and offers to retry instead.
   */
  if (today.isError && !data) {
    return (
      <div className="mx-auto max-w-2xl space-y-5">
        <h1 className="text-2xl font-semibold tracking-tight">{greeting()}</h1>
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-10 text-center">
            <CloudOff className="size-10 text-muted-foreground" />
            <p className="font-medium">We couldn't load your week</p>
            <p className="max-w-xs text-sm text-muted-foreground">
              Your training is safe — we just can't reach the gym right now.
            </p>
            <Button variant="outline" className="mt-2" onClick={() => today.refetch()} disabled={today.isFetching}>
              <RotateCw className={today.isFetching ? 'size-4 animate-spin' : 'size-4'} />
              {today.isFetching ? 'Trying…' : 'Try again'}
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {greeting()}
          {data ? `, ${data.firstName}` : ''}
        </h1>
        {today.isLoading ? (
          <Skeleton className="mt-1 h-5 w-56" />
        ) : (
          <p className="text-sm text-muted-foreground">
            {goalMet
              ? "You've hit your goal for the week. Anything else is a bonus."
              : sessions === 0
                ? "Let's get the week started."
                : `${remaining} more session${remaining === 1 ? '' : 's'} to hit your week.`}
          </p>
        )}
      </div>

      {/* Hero: the whole "am I on track" answer, without reading a single number twice. */}
      <Card>
        <CardContent className="flex flex-col items-center gap-5 py-6 sm:flex-row sm:justify-center sm:gap-8">
          {today.isLoading ? (
            <Skeleton className="size-40 rounded-full" />
          ) : (
            <ActivityRing
              value={sessions}
              goal={goal}
              colorClassName={goalMet ? 'text-emerald-500' : 'text-primary'}
            >
              <span className="text-4xl leading-none font-bold tabular-nums">{sessions}</span>
              <span className="mt-1 text-xs text-muted-foreground">of {goal} this week</span>
            </ActivityRing>
          )}

          <div className="flex flex-col items-center gap-1 sm:items-start">
            <span className="flex items-center gap-2">
              <Flame className={`size-7 ${streakWeeks > 0 ? 'text-orange-500' : 'text-muted-foreground'}`} />
              <span className="text-4xl leading-none font-bold tabular-nums">{streakWeeks}</span>
            </span>
            <span className="text-sm text-muted-foreground">week streak</span>
            {streakWeeks > 0 && !goalMet && (
              <span className="text-xs text-muted-foreground">Train this week to keep it alive</span>
            )}
            {data && (
              <Button
                variant="ghost"
                // Visually secondary, but still a real touch target: everything else a member taps
                // in this shell clears 44px, and this sits right under a 160px ring on a phone.
                className="mt-1 h-11 px-3 text-xs text-muted-foreground"
                onClick={() => setEditingGoal(true)}
              >
                <Pencil className="size-3.5" />
                Goal: {goal}/week
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* The one action this screen exists for. */}
      <Button asChild className="h-16 w-full text-base">
        <Link to="/log-activity">
          <Dumbbell className="size-5" />
          Log today's workout
        </Link>
      </Button>

      {data?.nextClassToday && (
        <Link
          to="/my-classes"
          className="flex items-center gap-3 rounded-xl border p-4 transition-colors hover:bg-accent"
        >
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10">
            <CalendarDays className="size-5 text-primary" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-xs font-medium tracking-wide text-muted-foreground uppercase">Today</span>
            <span className="block truncate font-medium">
              {classTimeFormat.format(new Date(data.nextClassToday.startsAt))} · {data.nextClassToday.classTypeName}
            </span>
          </span>
          <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
        </Link>
      )}

      {/* A single nudge — the recommendation engine already explains itself, so one line is enough. */}
      {data?.topRecommendation && (
        <Link
          to="/my-training"
          className="flex items-start gap-3 rounded-xl border p-4 transition-colors hover:bg-accent"
        >
          <Lightbulb className="mt-0.5 size-5 shrink-0 text-amber-500" />
          <span className="min-w-0 flex-1">
            <span className="block font-medium">{data.topRecommendation.title}</span>
            <span className="block text-sm text-muted-foreground">{data.topRecommendation.explanation}</span>
          </span>
          <ChevronRight className="mt-0.5 size-5 shrink-0 text-muted-foreground" />
        </Link>
      )}

      <WeeklyGoalDialog open={editingGoal} onOpenChange={setEditingGoal} currentGoal={goal} />
    </div>
  )
}

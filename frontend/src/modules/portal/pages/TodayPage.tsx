import { Link } from 'react-router-dom'
import { CalendarDays, ChevronRight, Dumbbell, Flame, Lightbulb } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ActivityRing } from '@/shared/components/ActivityRing'
import { useAuthStore } from '@/stores/authStore'
import {
  useMyClassBookings,
  useMyProfile,
  useMyRecommendations,
  useMyStreaks,
  useMyTrainingVolume,
} from '@/modules/portal/api/portalApi'

/**
 * Sessions per week the ring closes at.
 *
 * Hardcoded for now, deliberately: making this member-adjustable needs a stored preference, and
 * smuggling a backend change into a screen rebuild would blur what this phase actually changed.
 * Three is the standard "consistent trainee" baseline and matches the seeded demo member's cadence.
 */
const WEEKLY_SESSION_GOAL = 3

const classTimeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })

function greeting(): string {
  const h = new Date().getHours()
  if (h < 12) return 'Good morning'
  if (h < 18) return 'Good afternoon'
  return 'Good evening'
}

/** Monday-start week, matching the StreakCalculator the backend uses so the two never disagree. */
function startOfWeek(today: Date): Date {
  const d = new Date(today)
  d.setHours(0, 0, 0, 0)
  d.setDate(d.getDate() - ((d.getDay() + 6) % 7))
  return d
}

/**
 * The member's home. Previously this route showed membership status and a member code — real
 * information, but back-office information, and it forced a member to scroll past their admin
 * details to reach anything they'd open the app for. That content now lives on More > Membership.
 *
 * What's left answers one question on sight — "am I on track this week?" — and offers the single
 * action a member is here to take. Everything else is one tap away.
 */
export default function TodayPage() {
  const user = useAuthStore((s) => s.user)
  const profile = useMyProfile()
  const streaks = useMyStreaks()
  const bookings = useMyClassBookings()
  const recommendations = useMyRecommendations()
  // 7 days always covers the current Monday-start week, whatever day it is today.
  const volume = useMyTrainingVolume(7)

  const firstName = profile.data?.firstName ?? user?.firstName ?? 'there'

  const weekStart = startOfWeek(new Date())
  const sessionsThisWeek = (volume.data ?? []).filter(
    (d) => d.volumeKg > 0 && new Date(`${d.date}T00:00:00`) >= weekStart,
  ).length

  const streakWeeks = streaks.data?.workoutWeeks ?? 0
  const remaining = Math.max(0, WEEKLY_SESSION_GOAL - sessionsThisWeek)
  const goalMet = sessionsThisWeek >= WEEKLY_SESSION_GOAL

  const todaysClass = (bookings.data ?? []).find((b) => {
    const start = new Date(b.startsAt)
    const now = new Date()
    return start.toDateString() === now.toDateString() && start >= now
  })

  const nudge = recommendations.data?.[0]

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {greeting()}, {firstName}
        </h1>
        <p className="text-sm text-muted-foreground">
          {goalMet
            ? "You've hit your goal for the week. Anything else is a bonus."
            : remaining === WEEKLY_SESSION_GOAL
              ? "Let's get the week started."
              : `${remaining} more session${remaining === 1 ? '' : 's'} to hit your week.`}
        </p>
      </div>

      {/* Hero: the whole "am I on track" answer, without reading a single number twice. */}
      <Card>
        <CardContent className="flex flex-col items-center gap-5 py-6 sm:flex-row sm:justify-center sm:gap-8">
          {volume.isLoading ? (
            <Skeleton className="size-40 rounded-full" />
          ) : (
            <ActivityRing
              value={sessionsThisWeek}
              goal={WEEKLY_SESSION_GOAL}
              colorClassName={goalMet ? 'text-emerald-500' : 'text-primary'}
            >
              <span className="text-4xl leading-none font-bold tabular-nums">{sessionsThisWeek}</span>
              <span className="mt-1 text-xs text-muted-foreground">of {WEEKLY_SESSION_GOAL} this week</span>
            </ActivityRing>
          )}

          <div className="flex flex-col items-center gap-1 sm:items-start">
            <span className="flex items-center gap-2">
              <Flame className={`size-7 ${streakWeeks > 0 ? 'text-orange-500' : 'text-muted-foreground'}`} />
              <span className="text-4xl leading-none font-bold tabular-nums">{streakWeeks}</span>
            </span>
            <span className="text-sm text-muted-foreground">
              {streakWeeks === 1 ? 'week streak' : 'week streak'}
            </span>
            {streakWeeks > 0 && !goalMet && (
              <span className="text-xs text-muted-foreground">Train this week to keep it alive</span>
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

      {todaysClass && (
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
              {classTimeFormat.format(new Date(todaysClass.startsAt))} · {todaysClass.classTypeName}
            </span>
          </span>
          <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
        </Link>
      )}

      {/* A single nudge — the recommendation engine already explains itself, so one line is enough. */}
      {nudge && (
        <Link
          to="/my-training"
          className="flex items-start gap-3 rounded-xl border p-4 transition-colors hover:bg-accent"
        >
          <Lightbulb className="mt-0.5 size-5 shrink-0 text-amber-500" />
          <span className="min-w-0 flex-1">
            <span className="block font-medium">{nudge.title}</span>
            <span className="block text-sm text-muted-foreground">{nudge.explanation}</span>
          </span>
          <ChevronRight className="mt-0.5 size-5 shrink-0 text-muted-foreground" />
        </Link>
      )}
    </div>
  )
}

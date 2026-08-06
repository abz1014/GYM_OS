import { useState } from 'react'
import { Dumbbell, Flame, Sparkles, Trophy, Users } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { MemberEmptyState } from '@/modules/portal/components/portalShared'
import {
  useMyLeaderboard,
  type LeaderboardCategory,
  type LeaderboardPeriod,
  type LeaderboardRow,
} from '@/modules/portal/api/portalApi'

const CATEGORIES: { key: LeaderboardCategory; label: string; unit: string }[] = [
  { key: 'XpEarned', label: 'XP', unit: 'XP' },
  { key: 'WorkoutsLogged', label: 'Workouts', unit: 'days' },
  { key: 'GymVisits', label: 'Visits', unit: 'days' },
  { key: 'WeeklyStreak', label: 'Streak', unit: 'weeks' },
]

/** Below this many ranked members, a percentage says less than the raw field size. */
const MIN_RANKED_FOR_PERCENTILE = 20

/** Medal colours for the top three. Everyone below reads as ordinary text on purpose. */
const PODIUM_STYLE = ['text-amber-500', 'text-slate-400', 'text-amber-700']

function Row({ row, unit }: { row: LeaderboardRow; unit: string }) {
  return (
    <div
      className={`flex items-center gap-3 border-b p-3 last:border-b-0 ${
        row.isYou ? 'bg-primary/5' : ''
      }`}
    >
      <span className="flex w-8 shrink-0 justify-center">
        {row.rank <= 3 ? (
          <Trophy className={`size-5 ${PODIUM_STYLE[row.rank - 1]}`} />
        ) : (
          <span className="text-sm font-semibold tabular-nums text-muted-foreground">{row.rank}</span>
        )}
      </span>
      <span className="min-w-0 flex-1 truncate font-medium">
        {row.displayName}
        {row.isYou && <span className="ml-2 text-xs font-normal text-primary">You</span>}
      </span>
      <span className="shrink-0 font-semibold tabular-nums">{row.score.toLocaleString()}</span>
      <span className="w-10 shrink-0 text-xs text-muted-foreground">{unit}</span>
    </div>
  )
}

/**
 * Where a member stands against the people they actually train alongside.
 *
 * Branch-scoped and first-name-plus-initial by design (see LeaderboardPolicy) — a leaderboard is the
 * one place this app shows a member to other members, so it shows the least that still identifies
 * someone you'd recognise at the squat rack.
 *
 * Four categories rather than one because a single board only motivates whoever is winning it: the
 * heaviest lifter, the most consistent attender and the longest streak are different people, and
 * each should be able to find a board they're near the top of.
 */
export default function LeaderboardPage() {
  const [category, setCategory] = useState<LeaderboardCategory>('XpEarned')
  const [period, setPeriod] = useState<LeaderboardPeriod>('Month')
  const board = useMyLeaderboard(category, period)

  const unit = CATEGORIES.find((c) => c.key === category)!.unit
  const data = board.data

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Leaderboard</h1>
        <p className="text-sm text-muted-foreground">How you're doing against your gym this {period.toLowerCase()}.</p>
      </div>

      {/* Category first: it changes what the board means, so it's the primary control. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {CATEGORIES.map((c) => (
          <Button
            key={c.key}
            variant={c.key === category ? 'default' : 'outline'}
            className="h-11 w-full px-3 text-sm"
            onClick={() => setCategory(c.key)}
          >
            {c.key === 'XpEarned' && <Sparkles className="size-4" />}
            {c.key === 'WorkoutsLogged' && <Dumbbell className="size-4" />}
            {c.key === 'GymVisits' && <Users className="size-4" />}
            {c.key === 'WeeklyStreak' && <Flame className="size-4" />}
            {c.label}
          </Button>
        ))}
      </div>

      {/* A streak is a standing run, not something accumulated in a window, so the period toggle
          would be a lie on that board — hide it rather than show a control that does nothing. */}
      {category !== 'WeeklyStreak' && (
        <div className="flex gap-2">
          {(['Month', 'Week'] as LeaderboardPeriod[]).map((p) => (
            <Button
              key={p}
              variant={p === period ? 'secondary' : 'ghost'}
              className="h-11 flex-1 text-sm"
              onClick={() => setPeriod(p)}
            >
              This {p.toLowerCase()}
            </Button>
          ))}
        </div>
      )}

      {board.isLoading && <Skeleton className="h-64 w-full rounded-xl" />}

      {board.isError && !data && (
        <MemberEmptyState icon={Trophy} title="We couldn't load the board" hint="Check your connection and try again." />
      )}

      {data && data.totalRanked === 0 && (
        <MemberEmptyState
          icon={Trophy}
          title="Nobody's on the board yet"
          hint="Be the first — log a session and you'll take the top spot."
          action={{ label: 'Log a workout', to: '/log-activity' }}
        />
      )}

      {data && data.totalRanked > 0 && (
        <>
          {/* Your standing, stated before the list — most members open this to find themselves. */}
          <Card>
            <CardContent className="flex items-center justify-between gap-4 py-5">
              {data.you ? (
                <>
                  <div>
                    <p className="text-sm text-muted-foreground">Your rank</p>
                    <p className="text-4xl leading-none font-black tracking-tight tabular-nums">
                      #{data.you.rank}
                    </p>
                  </div>
                  <div className="text-right">
                    {/*
                      "Top X%" only once the board is big enough for a percentage to mean anything.
                      At a gym with four ranked members, first place is mathematically "top 25%",
                      which reads as an insult rather than a win — so small boards show the field
                      size instead, which is honest at any scale.
                    */}
                    {data.totalRanked >= MIN_RANKED_FOR_PERCENTILE ? (
                      <>
                        <p className="text-sm text-muted-foreground">Top</p>
                        <p className="text-4xl leading-none font-black tracking-tight tabular-nums text-primary">
                          {Math.max(1, Math.round((data.you.rank / data.totalRanked) * 100))}%
                        </p>
                      </>
                    ) : (
                      <>
                        <p className="text-sm text-muted-foreground">Out of</p>
                        <p className="text-4xl leading-none font-black tracking-tight tabular-nums text-primary">
                          {data.totalRanked}
                        </p>
                      </>
                    )}
                  </div>
                </>
              ) : (
                <div>
                  <p className="font-medium">You're not on this board yet</p>
                  <p className="text-sm text-muted-foreground">
                    Log a session and you'll appear from the next refresh.
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <section className="space-y-2">
            <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
              Top {Math.min(3, data.podium.length)} of {data.totalRanked}
            </h2>
            <div className="overflow-hidden rounded-xl border">
              {data.podium.map((r) => (
                <Row key={`podium-${r.rank}-${r.displayName}`} row={r} unit={unit} />
              ))}
            </div>
          </section>

          {data.aroundYou.length > 0 && (
            <section className="space-y-2">
              <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">Around you</h2>
              <div className="overflow-hidden rounded-xl border">
                {data.aroundYou.map((r) => (
                  <Row key={`near-${r.rank}-${r.displayName}`} row={r} unit={unit} />
                ))}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  )
}

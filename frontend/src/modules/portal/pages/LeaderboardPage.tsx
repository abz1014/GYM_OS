import { useState } from 'react'
import { Trophy } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { Bloom, GrainOverlay } from '@/shared/components/uplift'
import { MemberEmptyState } from '@/modules/portal/components/portalShared'
import {
  useMyLeaderboard,
  type LeaderboardCategory,
  type LeaderboardPeriod,
  type LeaderboardRow,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

const CATEGORIES: { key: LeaderboardCategory; label: string; unit: string }[] = [
  { key: 'XpEarned', label: 'XP', unit: 'XP' },
  { key: 'WorkoutsLogged', label: 'Workouts', unit: 'days' },
  { key: 'GymVisits', label: 'Visits', unit: 'days' },
  { key: 'WeeklyStreak', label: 'Streak', unit: 'weeks' },
]

/** Below this many ranked members, a percentage says less than the raw field size. */
const MIN_RANKED_FOR_PERCENTILE = 20

/** Initials for the row avatar. Names arrive as "First L." (see LeaderboardPolicy). */
function initials(displayName: string): string {
  return displayName
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join('')
}

function Row({ row, unit }: { row: LeaderboardRow; unit: string }) {
  return (
    <li
      className={cn(
        'flex items-center gap-3 rounded-2xl border p-3',
        row.isYou
          ? // Inverted rather than merely tinted: a member scrolling for themselves should find their
            // own row without reading a single name.
            'border-primary bg-primary text-primary-foreground shadow-volt'
          : row.rank === 1
            ? 'border-warning/40 bg-card'
            : 'border-border bg-card',
      )}
    >
      <span
        className={cn(
          'w-7 shrink-0 text-right font-display text-lg font-black tabular-nums',
          row.isYou ? 'text-primary-foreground' : row.rank === 1 ? 'text-warning' : 'text-muted-foreground',
        )}
      >
        {row.rank}
      </span>
      <span
        className={cn(
          'flex size-9 shrink-0 items-center justify-center rounded-xl text-xs font-bold',
          row.isYou
            ? 'bg-primary-foreground text-primary'
            : row.rank === 1
              ? 'bg-warning/15 text-warning'
              : 'bg-muted text-muted-foreground',
        )}
      >
        {initials(row.displayName)}
      </span>
      <span className="min-w-0 flex-1 truncate font-medium">{row.isYou ? 'You' : row.displayName}</span>
      <span className="shrink-0 font-display font-black tabular-nums">{row.score.toLocaleString()}</span>
      <span className={cn('w-9 shrink-0 text-xs', row.isYou ? 'opacity-70' : 'text-muted-foreground')}>{unit}</span>
    </li>
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
 *
 * The redesign proposed fusing this with Challenges into one screen. Kept separate: a challenge is
 * something a member opted into with an end date, a leaderboard is a standing comparison that always
 * exists — and the gap sentence the merged design leads with ("one more heavy session should do it")
 * needs pace-to-target maths that only a challenge has. The visual language is shared instead.
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
        <h1 className="font-display text-3xl font-black tracking-tight">Leaderboard</h1>
        <p className="text-sm text-muted-foreground">How you're doing against your gym this {period.toLowerCase()}.</p>
      </div>

      {/* Category first: it changes what the board means, so it's the primary control. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {CATEGORIES.map((c) => (
          <Button
            key={c.key}
            variant={c.key === category ? 'default' : 'outline'}
            className="h-11 w-full rounded-xl px-3 text-sm font-bold"
            onClick={() => setCategory(c.key)}
          >
            {c.label}
          </Button>
        ))}
      </div>

      {/* A streak is a standing run, not something accumulated in a window, so the period toggle
          would be a lie on that board — hide it rather than show a control that does nothing. */}
      {category !== 'WeeklyStreak' && (
        <div className="flex gap-1 rounded-2xl bg-muted p-1">
          {(['Month', 'Week'] as LeaderboardPeriod[]).map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => setPeriod(p)}
              className={cn(
                'h-10 flex-1 rounded-xl text-sm font-bold transition-colors',
                p === period ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              This {p.toLowerCase()}
            </button>
          ))}
        </div>
      )}

      {board.isLoading && <Skeleton className="h-64 w-full rounded-3xl" />}

      {isStale(board) && !data && (
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
          <div
            className="relative overflow-hidden rounded-3xl border border-border p-5 edge-light"
            style={{ background: 'linear-gradient(160deg,#17190F,#151517 46%,#121214)' }}
          >
            <Bloom className="-top-14 -left-8 size-52" opacity={0.16} />
            <GrainOverlay />
            {data.you ? (
              <div className="flex items-end justify-between gap-4">
                <div>
                  <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
                    Your position
                  </p>
                  <p className="mt-1 flex items-baseline gap-2">
                    <span
                      className="font-display text-6xl leading-none font-black tracking-tight text-primary tabular-nums"
                      // The one numeral on this screen a member is looking for; the glow is what
                      // makes it findable without reading.
                      style={{ textShadow: '0 0 28px rgb(214 249 74 / 0.45)' }}
                    >
                      {data.you.rank}
                    </span>
                    <span className="text-lg text-muted-foreground">of {data.totalRanked}</span>
                  </p>
                </div>
                <div className="text-right">
                  {/*
                    "Top X%" only once the board is big enough for a percentage to mean anything.
                    At a gym with four ranked members, first place is mathematically "top 25%",
                    which reads as an insult rather than a win — so small boards show the raw score
                    instead, which is honest at any scale.
                  */}
                  {data.totalRanked >= MIN_RANKED_FOR_PERCENTILE ? (
                    <>
                      <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Top</p>
                      <p className="font-display text-3xl leading-none font-black tracking-tight tabular-nums">
                        {Math.max(1, Math.round((data.you.rank / data.totalRanked) * 100))}%
                      </p>
                    </>
                  ) : (
                    <>
                      <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">{unit}</p>
                      <p className="font-display text-3xl leading-none font-black tracking-tight tabular-nums">
                        {data.you.score.toLocaleString()}
                      </p>
                    </>
                  )}
                </div>
              </div>
            ) : (
              <div>
                <p className="font-medium">You're not on this board yet</p>
                <p className="text-sm text-muted-foreground">
                  Log a session and you'll appear from the next refresh.
                </p>
              </div>
            )}
          </div>

          <section className="space-y-2">
            <h2 className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
              Top {Math.min(3, data.podium.length)} of {data.totalRanked}
            </h2>
            <ul className="space-y-2">
              {data.podium.map((r) => (
                <Row key={`podium-${r.rank}-${r.displayName}`} row={r} unit={unit} />
              ))}
            </ul>
          </section>

          {data.aroundYou.length > 0 && (
            <section className="space-y-2">
              {/* The break in rank between the podium above and the member's neighbours below, said
                  as a gap rather than left as an unexplained jump from 3 to 19. */}
              <p aria-hidden className="text-center text-lg tracking-[0.3em] text-muted-foreground">···</p>
              <ul className="space-y-2">
                {data.aroundYou.map((r) => (
                  <Row key={`near-${r.rank}-${r.displayName}`} row={r} unit={unit} />
                ))}
              </ul>
            </section>
          )}
        </>
      )}
    </div>
  )
}

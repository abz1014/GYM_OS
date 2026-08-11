import { Link } from 'react-router-dom'
import { Clock, Lock, TrendingUp, Trophy } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { CountUp } from '@/shared/components/uplift'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
import { useMyExperience, type MyRank, type RankTier } from '@/modules/portal/api/portalApi'

/**
 * The ladder, in the order RankPolicy defines it. Kept as a plain list rather than derived from the
 * response because the whole point of the screen is showing a member what is ABOVE them — the API
 * reports where they stand, not the shape of the ladder they are standing on.
 *
 * The XP figures are RankPolicy's thresholds. They are duplicated here, which is a real cost and the
 * lesser one: the alternative is a screen that cannot say "6,000 XP" about a rung the member has not
 * reached, and a ladder whose upper rungs are unlabelled is exactly the dry number this replaces.
 * If the thresholds move, this list moves with them.
 */
const LADDER: { tier: RankTier; xp: number }[] = [
  { tier: 'Newcomer', xp: 0 },
  { tier: 'Regular', xp: 750 },
  { tier: 'Committed', xp: 2_500 },
  { tier: 'Strong', xp: 6_000 },
  { tier: 'Relentless', xp: 12_000 },
  { tier: 'Elite', xp: 20_000 },
  { tier: 'Titan', xp: 32_000 },
  { tier: 'Legend', xp: 50_000 },
]

/**
 * What each rung feels like, in one line.
 *
 * This is the "how will the software celebrate it" answer, and it is deliberately about what the
 * member DID rather than what they unlock — there is no reward catalogue behind this, and inventing
 * one ("free smoothie at Titan!") would be the app writing cheques the gym never agreed to.
 */
const TIER_BLURB: Record<RankTier, string> = {
  Newcomer: 'Everyone starts here. One session and you are moving.',
  Regular: 'You have made it a habit rather than an intention.',
  Committed: 'Months of showing up. This is where most people stop — and you did not.',
  Strong: 'The work is visible now. Half a year of it.',
  Relentless: 'A year of turning up whether or not you felt like it.',
  Elite: 'Very few members reach this. You are one of them.',
  Titan: 'Years of it. The gym knows your name without checking.',
  Legend: 'The top of the ladder. There is nothing above this.',
}

const TIER_STYLE: Record<RankTier, string> = {
  Newcomer: 'text-muted-foreground',
  Regular: 'text-sky-400',
  Committed: 'text-emerald-400',
  Strong: 'text-primary',
  Relentless: 'text-amber-400',
  Elite: 'text-orange-400',
  Titan: 'text-fuchsia-400',
  Legend: 'text-yellow-300',
}

function RankBadge({ tier, size = 'lg' }: { tier: RankTier; size?: 'lg' | 'sm' }) {
  return (
    <span
      className={cn(
        'font-display font-black tracking-tight uppercase',
        TIER_STYLE[tier],
        size === 'lg' ? 'text-4xl' : 'text-sm',
      )}
    >
      {tier}
    </span>
  )
}

/**
 * The headline. Peak is the identity; current is only mentioned when it differs, because for a member
 * training normally the two are equal and printing both would invent a distinction they do not have.
 */
function StandingCard({ rank, totalXp }: { rank: MyRank; totalXp: number }) {
  const slipped = rank.tiersLostToAbsence > 0
  const pct = rank.tierSpan > 0 ? Math.min(100, Math.round((rank.xpIntoTier / rank.tierSpan) * 100)) : 100

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="space-y-4 py-6 text-center">
        <p className="text-[11px] font-bold tracking-[0.16em] text-muted-foreground uppercase">
          {slipped ? 'Your peak rank' : 'Your rank'}
        </p>
        <RankBadge tier={rank.peak} />
        <p className="text-sm text-muted-foreground">{TIER_BLURB[rank.peak]}</p>

        <div className="space-y-1.5 pt-2">
          <div className="flex items-baseline justify-between text-sm">
            <span className="font-semibold tabular-nums">
              <CountUp to={totalXp} /> XP
            </span>
            {rank.next ? (
              <span className="text-muted-foreground tabular-nums">
                {rank.xpToNextTier.toLocaleString()} to {rank.next}
              </span>
            ) : (
              <span className="text-muted-foreground">Top of the ladder</span>
            )}
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div
              className="h-full rounded-full bg-primary transition-[width] duration-(--duration-expressive) ease-(--ease-uplift)"
              style={{ width: `${pct}%` }}
            />
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

/**
 * Shown only when absence has actually cost something.
 *
 * The copy is an invitation, not a scolding, and that is the deliberate departure from the games this
 * borrows from. RankPolicy restores the peak on the first session back, so the honest sentence is
 * "one session gets it back" — and a member reading it at the moment they are deciding whether to
 * return should meet a door held open rather than a bill.
 */
function SlippedCard({ rank }: { rank: MyRank }) {
  return (
    <Card className="rounded-3xl border-amber-500/30 bg-amber-500/5">
      <CardContent className="space-y-2 py-5">
        <p className="flex items-center gap-2 text-sm font-semibold text-amber-400">
          <Clock className="size-4" />
          Standing at <RankBadge tier={rank.current} size="sm" /> while you are away
        </p>
        <p className="text-sm text-muted-foreground">
          Your peak rank of {rank.peak} is yours permanently — nothing takes that back. One session
          puts you straight back to it.
        </p>
        <Button asChild size="sm" className="mt-1">
          <Link to="/workout">Start a session</Link>
        </Button>
      </CardContent>
    </Card>
  )
}

/** The rungs, so a member can see what is above them and what it costs. */
function Ladder({ rank, totalXp }: { rank: MyRank; totalXp: number }) {
  const peakIndex = LADDER.findIndex((r) => r.tier === rank.peak)

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <p className="mb-3 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
          The ladder
        </p>
        <ul className="space-y-1">
          {LADDER.map((rung, i) => {
            const reached = i <= peakIndex
            const isPeak = i === peakIndex
            return (
              <li
                key={rung.tier}
                className={cn(
                  'flex items-center justify-between gap-3 rounded-xl px-3 py-2',
                  isPeak && 'bg-primary/10',
                )}
              >
                <span className="flex min-w-0 items-center gap-2">
                  {reached ? (
                    <Trophy className={cn('size-4 shrink-0', TIER_STYLE[rung.tier])} />
                  ) : (
                    <Lock className="size-4 shrink-0 text-muted-foreground/50" />
                  )}
                  <span
                    className={cn(
                      'truncate text-sm font-medium',
                      reached ? TIER_STYLE[rung.tier] : 'text-muted-foreground',
                    )}
                  >
                    {rung.tier}
                  </span>
                  {isPeak && (
                    <span className="text-[10px] font-bold tracking-wider text-primary uppercase">You</span>
                  )}
                </span>
                <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                  {reached ? 'Reached' : `${(rung.xp - totalXp).toLocaleString()} XP away`}
                </span>
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}

/**
 * "Where do I stand?" — the member's status home.
 *
 * It replaced My Progress in the tab bar rather than joining it, because the bar carries four flat
 * tabs either side of the centre action and a fifth pushes that action off-centre. Progress moved to
 * More and is linked from the bottom of this page: rank is the headline, the charts are the detail
 * behind it.
 */
export default function MyRankPage() {
  const experience = useMyExperience()

  return (
    <div className="space-y-4">
      <div>
        <h1 className="font-display text-2xl font-black tracking-tight">Rank</h1>
        <p className="text-sm text-muted-foreground">What you have reached, and what is next.</p>
      </div>

      {experience.isLoading && <Skeleton className="h-64 w-full rounded-3xl" />}

      {experience.isError && (
        <MemberLoadError
          title="We couldn't load your rank"
          onRetry={() => void experience.refetch()}
          isRetrying={experience.isFetching}
        />
      )}

      {experience.data && (
        <>
          <StandingCard rank={experience.data.rank} totalXp={experience.data.totalXp} />

          {experience.data.rank.tiersLostToAbsence > 0 && <SlippedCard rank={experience.data.rank} />}

          {/* Only when a drop is genuinely coming. A countdown shown to somebody who trained
              yesterday would be a threat invented out of nothing. */}
          {experience.data.rank.daysUntilNextDemotion !== null && (
            <p className="px-1 text-sm text-muted-foreground">
              Train within{' '}
              <span className="font-semibold text-foreground tabular-nums">
                {experience.data.rank.daysUntilNextDemotion}
              </span>{' '}
              {experience.data.rank.daysUntilNextDemotion === 1 ? 'day' : 'days'} to hold your current
              standing.
            </p>
          )}

          <Ladder rank={experience.data.rank} totalXp={experience.data.totalXp} />

          <Button asChild variant="outline" className="w-full">
            <Link to="/my-progress">
              <TrendingUp className="size-4" />
              See the detail behind it
            </Link>
          </Button>
        </>
      )}
    </div>
  )
}

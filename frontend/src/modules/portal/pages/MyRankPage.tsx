import { Link } from 'react-router-dom'
import {
  Activity, Apple, Award, CalendarCheck, Check, ChevronRight, Clock, DoorOpen, Dumbbell, Flag,
  Flame, Lock, MapPin, Moon, TrendingUp, Trophy, Zap,
} from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { CountUp } from '@/shared/components/uplift'
import { MemberLoadError, RANK_TIER_LINE } from '@/modules/portal/components/portalShared'
import { RankUpCelebration } from '@/modules/portal/components/RankUpCelebration'
import {
  useAcknowledgeRankPromotion,
  useMyAchievements,
  useMyExperience,
  useMyRankLadder,
  type ClimbTip,
  type MyAchievement,
  type MyRank,
  type MyRankLadder,
  type RankTier,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

const promotionDate = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

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

/** The glow behind the headline badge. One per tier, so the rung is felt before it is read. */
const TIER_GLOW: Record<RankTier, string> = {
  Newcomer: 'rgb(148 163 184 / 0.28)',
  Regular: 'rgb(56 189 248 / 0.32)',
  Committed: 'rgb(52 211 153 / 0.32)',
  Strong: 'rgb(214 249 74 / 0.34)',
  Relentless: 'rgb(251 191 36 / 0.34)',
  Elite: 'rgb(251 146 60 / 0.36)',
  Titan: 'rgb(232 121 249 / 0.36)',
  Legend: 'rgb(253 224 71 / 0.42)',
}

/** Icons for the climb tips. Keyed by RankClimbPolicy's stable codes, never by the copy. */
const TIP_ICON: Record<string, typeof Zap> = {
  'first-session': Dumbbell,
  challenge: Trophy,
  'check-in': MapPin,
  progress: TrendingUp,
  nutrition: Apple,
  recovery: Moon,
}

/** Where a tip sends the member. A suggestion they cannot act on from here is only a scolding. */
const TIP_HREF: Record<string, string> = {
  'first-session': '/my-training',
  challenge: '/my-challenges',
  'check-in': '/portal',
  progress: '/workout',
  nutrition: '/my-nutrition',
  recovery: '/log-activity',
}

/**
 * AchievementDefinition.Icon is a lucide icon NAME — "dumbbell", "door-open" — not an emoji. Rendering
 * the field directly prints the word, which is what the first draft of this shelf did.
 *
 * Award is the fallback rather than a blank, so a badge the catalogue gains tomorrow shows up as an
 * unrecognised badge instead of a hole in the grid.
 */
const BADGE_ICON: Record<string, typeof Award> = {
  dumbbell: Dumbbell,
  trophy: Trophy,
  zap: Zap,
  activity: Activity,
  'door-open': DoorOpen,
  'calendar-check': CalendarCheck,
  flag: Flag,
}

const BADGE_TIER_STYLE: Record<MyAchievement['tier'], string> = {
  Bronze: 'text-amber-600',
  Silver: 'text-slate-300',
  Gold: 'text-yellow-400',
  Platinum: 'text-cyan-300',
}

function RankBadge({ tier, size = 'lg' }: { tier: RankTier; size?: 'lg' | 'sm' }) {
  return (
    <span
      className={cn(
        'font-display font-black tracking-tight uppercase',
        TIER_STYLE[tier],
        size === 'lg' ? 'text-4xl' : 'text-sm',
      )}
      style={size === 'lg' ? { textShadow: `0 0 34px ${TIER_GLOW[tier]}` } : undefined}
    >
      {tier}
    </span>
  )
}

/**
 * The headline: where you stand, what is next, and — the part that was missing — when you get there.
 *
 * Peak is the identity; current is only mentioned when it differs, because for a member training
 * normally the two are equal and printing both would invent a distinction they do not have.
 *
 * The pace line is the whole reason this screen stopped being inert. "5,500 XP to Strong" is a
 * distance with no scale attached; "about 9 weeks at your pace" is something a person can plan
 * around. It appears only when the server sent one — see RankClimbPolicy, which refuses to divide a
 * gap by a pace too small to mean anything rather than reporting a seven-year estimate to somebody
 * who has just come back.
 */
function StandingCard({
  rank, totalXp, ladder,
}: {
  rank: MyRank
  totalXp: number
  ladder: MyRankLadder | undefined
}) {
  const slipped = rank.tiersLostToAbsence > 0
  const pct = rank.tierSpan > 0 ? Math.min(100, Math.round((rank.xpIntoTier / rank.tierSpan) * 100)) : 100

  return (
    <Card className="relative overflow-hidden rounded-3xl edge-light">
      {/* A wash in the tier's own colour. Reaching a new rung should change what the screen LOOKS
          like, not only what it says — the badge alone is a word swap. */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-x-0 top-0 h-40"
        style={{ background: `radial-gradient(120% 100% at 50% 0%, ${TIER_GLOW[rank.peak]}, transparent 70%)` }}
      />
      <CardContent className="relative space-y-4 py-6 text-center">
        <p className="text-[11px] font-bold tracking-[0.16em] text-muted-foreground uppercase">
          {slipped ? 'Your peak rank' : 'Your rank'}
        </p>
        <RankBadge tier={rank.peak} />
        <p className="text-sm text-muted-foreground">{RANK_TIER_LINE[rank.peak]}</p>

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

        {ladder && rank.next && (
          <div className="flex items-center justify-center gap-2 pt-1 text-sm">
            <Zap className="size-4 shrink-0 text-primary" />
            {ladder.weeksToNextTier !== null ? (
              <span>
                <span className="font-display font-black tabular-nums">
                  ~{ladder.weeksToNextTier} {ladder.weeksToNextTier === 1 ? 'week' : 'weeks'}
                </span>{' '}
                <span className="text-muted-foreground">
                  to {rank.next} at {ladder.xpPerWeek.toLocaleString()} XP a week
                </span>
              </span>
            ) : (
              <span className="text-muted-foreground">
                Train a few times and we can tell you how far off {rank.next} is.
              </span>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * The race the member is actually in.
 *
 * The gym-wide leaderboard puts a Newcomer ninetieth behind eight Titans, which is a fact and not a
 * contest. Ranked among the people on their own rung, everyone is somewhere they could plausibly
 * move — and the gap to the person one place above is usually a session or two, which is the
 * smallest true number on this screen and the most motivating.
 */
function RungBoard({ ladder, tier }: { ladder: MyRankLadder; tier: RankTier }) {
  /*
   * Alone on the rung is a real and fairly common state — a small gym, an early launch, or simply
   * a member out ahead of everyone at their level. Rendering nothing leaves a hole where a section
   * was; saying it plainly is both true and, read at the right moment, quite good news.
   */
  if (ladder.onYourRung.length <= 1) {
    return (
      <Card className="rounded-3xl edge-light">
        <CardContent className="py-5 text-center">
          <Trophy className={cn('mx-auto size-6', TIER_STYLE[tier])} />
          <p className="mt-2 text-sm font-semibold">
            You are the only <span className={TIER_STYLE[tier]}>{tier}</span> at your branch.
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            Nobody to chase. Keep the rung to yourself.
          </p>
        </CardContent>
      </Card>
    )
  }

  const you = ladder.onYourRung.find((p) => p.isYou)
  // Top three always, plus the member and their immediate neighbours when they are further down.
  const shown = ladder.onYourRung.filter(
    (p) => p.position <= 3 || (you !== undefined && Math.abs(p.position - you.position) <= 1),
  )

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <div className="mb-1 flex items-baseline justify-between gap-2">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            Others at <span className={TIER_STYLE[tier]}>{tier}</span>
          </p>
          <span className="text-xs text-muted-foreground tabular-nums">
            {ladder.onYourRung.length} at your branch
          </span>
        </div>

        {ladder.chasing && (
          <p className="mb-3 text-sm">
            <span className="font-display font-black tabular-nums text-primary">
              {ladder.chasing.xpAhead.toLocaleString()} XP
            </span>{' '}
            <span className="text-muted-foreground">behind {ladder.chasing.displayName}.</span>
          </p>
        )}

        <ul className="space-y-1">
          {shown.map((peer, i) => {
            // A gap in the numbering is real information — it says there are people between these
            // two rows. Silently listing 3 then 9 would read as a bug.
            const gap = i > 0 && peer.position - shown[i - 1].position > 1
            return (
              <li key={peer.position}>
                {gap && <p className="py-1 text-center text-xs text-muted-foreground">···</p>}
                <div
                  className={cn(
                    'flex items-center justify-between gap-3 rounded-xl px-3 py-2',
                    peer.isYou && 'bg-primary/10',
                  )}
                >
                  <span className="flex min-w-0 items-center gap-3">
                    <span
                      className={cn(
                        'w-6 shrink-0 text-center font-display font-black tabular-nums',
                        peer.position === 1 ? 'text-primary' : 'text-muted-foreground',
                      )}
                    >
                      {peer.position}
                    </span>
                    <span className={cn('truncate text-sm', peer.isYou ? 'font-bold' : 'font-medium')}>
                      {peer.isYou ? 'You' : peer.displayName}
                    </span>
                  </span>
                  <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                    {peer.xp.toLocaleString()} XP
                  </span>
                </div>
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}

/**
 * How to climb, where every line is about this member.
 *
 * Each tip names a number out of their own last four weeks and carries what the action really pays,
 * so none of it is the app telling everybody the same thing. Each one is also a link: a suggestion
 * you cannot act on from where you are reading it is just a complaint.
 */
function ClimbTips({ tips, windowDays }: { tips: ClimbTip[]; windowDays: number }) {
  if (tips.length === 0) {
    return (
      <Card className="rounded-3xl border-primary/30 bg-primary/5">
        <CardContent className="py-5 text-center">
          <Flame className="mx-auto size-6 text-primary" />
          <p className="mt-2 text-sm font-semibold">You are earning from every source there is.</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Sessions, check-ins, records, nutrition, recovery and a challenge. Just keep going.
          </p>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
          How to climb faster
        </p>
        <p className="mt-1 mb-3 text-xs text-muted-foreground">
          Based on your last {windowDays} days.
        </p>
        <ul className="space-y-2">
          {tips.slice(0, 3).map((tip) => {
            const Icon = TIP_ICON[tip.code] ?? Zap
            return (
              <li key={tip.code}>
                <Link
                  to={TIP_HREF[tip.code] ?? '/portal'}
                  className="press flex items-center gap-3 rounded-2xl border border-border p-3 hover:border-primary/50"
                >
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-muted">
                    <Icon className="size-4 text-primary" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-display font-bold tracking-tight">{tip.title}</span>
                    <span className="block text-xs text-muted-foreground">{tip.detail}</span>
                  </span>
                  <span className="shrink-0 rounded-lg bg-primary/15 px-2 py-1 font-display text-xs font-black text-primary tabular-nums">
                    +{tip.xpValue}
                  </span>
                  <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                </Link>
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}

/**
 * The shelf. Locked badges are shown, dimmed, because the locked ones are the point — a shelf of
 * only what you already have is a receipt, and a shelf of what is still out there is a reason to
 * come back. The endpoint has returned all thirteen from the day it shipped; this is the first
 * screen to ask for them.
 */
function BadgeShelf({ achievements }: { achievements: MyAchievement[] }) {
  const unlocked = achievements.filter((a) => a.unlocked).length

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <div className="mb-3 flex items-baseline justify-between gap-2">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Badges</p>
          <span className="text-xs text-muted-foreground tabular-nums">
            {unlocked} of {achievements.length}
          </span>
        </div>
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {achievements.map((badge) => {
            const Icon = BADGE_ICON[badge.icon] ?? Award
            return (
            <li
              key={badge.code}
              title={badge.description}
              className={cn(
                'flex items-center gap-2 rounded-2xl border p-3',
                badge.unlocked ? 'border-border bg-card' : 'border-dashed border-border/60 opacity-55',
              )}
            >
              <span
                className={cn(
                  'flex size-9 shrink-0 items-center justify-center rounded-xl',
                  badge.unlocked ? 'bg-muted' : 'bg-transparent',
                )}
              >
                {badge.unlocked ? (
                  <Icon className={cn('size-4', BADGE_TIER_STYLE[badge.tier])} />
                ) : (
                  <Lock className="size-4 text-muted-foreground/60" />
                )}
              </span>
              <span className="min-w-0">
                <span
                  className={cn(
                    'block truncate text-sm font-bold',
                    badge.unlocked ? BADGE_TIER_STYLE[badge.tier] : 'text-muted-foreground',
                  )}
                >
                  {badge.name}
                </span>
                <span className="block truncate text-[11px] text-muted-foreground">
                  {badge.unlocked && badge.unlockedAt
                    ? promotionDate.format(new Date(badge.unlockedAt))
                    : badge.description}
                </span>
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

/**
 * The rungs, so a member can see what is above them and what it costs.
 *
 * The thresholds now come from the server. They used to be a copy of RankPolicy's table living in
 * this file, with a comment admitting the duplication — and a ladder that disagrees with the engine
 * tells a member they need 6,000 XP for a rung the server opens at 5,000.
 */
function Ladder({ rungs, totalXp }: { rungs: MyRankLadder['rungs']; totalXp: number }) {
  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <p className="mb-3 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
          The ladder
        </p>
        <ul className="space-y-1">
          {rungs.map((rung) => (
            <li
              key={rung.tier}
              className={cn(
                'flex items-center justify-between gap-3 rounded-xl px-3 py-2',
                rung.isYou && 'bg-primary/10',
              )}
            >
              <span className="flex min-w-0 items-center gap-2">
                {rung.reached ? (
                  <Trophy className={cn('size-4 shrink-0', TIER_STYLE[rung.tier])} />
                ) : (
                  <Lock className="size-4 shrink-0 text-muted-foreground/50" />
                )}
                <span
                  className={cn(
                    'truncate text-sm font-medium',
                    rung.reached ? TIER_STYLE[rung.tier] : 'text-muted-foreground',
                  )}
                >
                  {rung.tier}
                </span>
                {rung.isYou && (
                  <span className="text-[10px] font-bold tracking-wider text-primary uppercase">You</span>
                )}
              </span>
              <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                {rung.reached ? 'Reached' : `${(rung.xpRequired - totalXp).toLocaleString()} XP away`}
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

/**
 * The climb, kept.
 *
 * The point of recording promotions rather than flashing them: a member can come back months later
 * and see the dates they crossed each rung. A celebration you cannot revisit is a celebration that,
 * for anyone who had the app closed when it fired, never happened at all.
 */
function PromotionHistory({ rank }: { rank: MyRank }) {
  if (rank.promotions.length === 0) {
    return null
  }

  return (
    <Card className="rounded-3xl edge-light">
      <CardContent className="py-4">
        <p className="mb-3 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
          Your climb
        </p>
        <ul className="space-y-2">
          {rank.promotions.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-3 text-sm">
              <span className="flex min-w-0 items-center gap-2">
                <Check className={cn('size-4 shrink-0', TIER_STYLE[p.tier])} />
                <span className={cn('truncate font-medium', TIER_STYLE[p.tier])}>{p.tier}</span>
              </span>
              <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                {promotionDate.format(new Date(p.achievedAt))}
              </span>
            </li>
          ))}
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
 * More and is linked from this page.
 *
 * Three reads, three failure modes handled separately rather than as one screen-wide blank: rank is
 * the headline and the page cannot be drawn without it, but the rung board, the tips and the badges
 * are additions — a member whose badge request drops should still see their rank rather than an
 * error page, and a section that could not load says so where it would have been.
 */
export default function MyRankPage() {
  const experience = useMyExperience()
  const ladder = useMyRankLadder()
  const achievements = useMyAchievements()
  const acknowledge = useAcknowledgeRankPromotion()
  const unseen = experience.data?.rank.unseen ?? null

  return (
    <div className="space-y-4">
      {/* Server-owned, not local state: the promotion row carries `seen`, so closing the app before
          dismissing means the celebration is still waiting next time rather than silently spent. */}
      {unseen && (
        <RankUpCelebration
          promotion={unseen}
          onDismiss={() => acknowledge.mutate(unseen.id)}
        />
      )}

      <div>
        <h1 className="font-display text-2xl font-black tracking-tight">Rank</h1>
        <p className="text-sm text-muted-foreground">What you have reached, and what is next.</p>
      </div>

      {experience.isLoading && <Skeleton className="h-64 w-full rounded-3xl shimmer" />}

      {isStale(experience) && (
        <MemberLoadError
          title="We couldn't load your rank"
          onRetry={() => void experience.refetch()}
          isRetrying={experience.isFetching}
        />
      )}

      {experience.data && (
        <>
          <StandingCard
            rank={experience.data.rank}
            totalXp={experience.data.totalXp}
            ladder={ladder.data}
          />

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

          {ladder.isLoading && <Skeleton className="h-48 w-full rounded-3xl shimmer" />}

          {isStale(ladder) && (
            <MemberLoadError
              title="We couldn't load the ladder"
              hint="Your rank above is real — it's the standings and the tips we can't reach."
              onRetry={() => void ladder.refetch()}
              isRetrying={ladder.isFetching}
            />
          )}

          {ladder.data && (
            <>
              <RungBoard ladder={ladder.data} tier={experience.data.rank.peak} />
              <ClimbTips tips={ladder.data.tips} windowDays={ladder.data.paceWindowDays} />
              <Ladder rungs={ladder.data.rungs} totalXp={experience.data.totalXp} />
            </>
          )}

          {achievements.isLoading && <Skeleton className="h-40 w-full rounded-3xl shimmer" />}
          {achievements.data && achievements.data.length > 0 && (
            <BadgeShelf achievements={achievements.data} />
          )}

          <PromotionHistory rank={experience.data.rank} />

          {/* Was a ghost-weight outline button at the very bottom of the page, which is where a link
              goes to be missed. */}
          <Button asChild variant="secondary" className="press h-12 w-full rounded-2xl font-bold">
            <Link to="/my-progress">
              <TrendingUp className="size-4" />
              See the detail behind it
              <ChevronRight className="size-4" />
            </Link>
          </Button>
        </>
      )}
    </div>
  )
}

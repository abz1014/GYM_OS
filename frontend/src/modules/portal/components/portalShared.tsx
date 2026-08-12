/**
 * Shared building blocks for the member portal pages. These used to live inside a single 801-line
 * MemberPortalPage; they were lifted out when that page was split into focused screens so the
 * styling of a mastery bar, a macro bar or a challenge row stays identical wherever it appears.
 */
import { BarChart3, ClipboardList, CloudOff, Flag, HeartPulse, Loader2, Minus, RotateCw, Shuffle, Sparkles, TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import type { AchievementTier, OverloadSuggestion, RankTier, RecommendationType, RecoveryStatus } from '@/modules/portal/api/portalApi'
import { useJoinChallenge, useLeaveChallenge, type MyChallenge } from '@/modules/challenges/api/challengesApi'

/**
 * An empty member screen, phrased as a starting point rather than an absence.
 *
 * The difference matters more here than in the staff console. A member reaching an empty screen is
 * usually new or returning after a lapse — exactly the moment the retention research says people
 * quit — and "No workout plan assigned yet." reads as a dead end reported by a database. Every empty
 * state gets a way forward instead: what this space is for, and the one tap that fills it.
 */
export function MemberEmptyState({
  icon: Icon,
  title,
  hint,
  action,
}: {
  icon: LucideIcon
  title: string
  hint?: string
  action?: { label: string; to: string }
}) {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center">
      <span className="flex size-12 items-center justify-center rounded-full bg-primary/10">
        <Icon className="size-6 text-primary" />
      </span>
      <p className="font-medium">{title}</p>
      {hint && <p className="max-w-xs text-sm text-muted-foreground">{hint}</p>}
      {action && (
        <Button asChild variant="outline" className="mt-2 h-11 px-4">
          <Link to={action.to}>{action.label}</Link>
        </Button>
      )}
    </div>
  )
}

/**
 * The other empty screen: the one where we don't know.
 *
 * MemberEmptyState above is a claim about the member — "you haven't logged a workout yet", "your
 * trainer hasn't set you a plan". Those sentences were being printed by dropped requests all over
 * this module, because every one of them was reached through `(data ?? []).length === 0` and a
 * failed query produces the same empty array as an empty gym. Told that their trainer has given them
 * nothing, or that none of their training exists, a member has no way to know it isn't true — and on
 * the logging screens they act on it and record the session twice.
 *
 * So the two states are separate components, phrased to be unmistakable: this one talks about the
 * connection and never about the member, and it always offers the retry, because every portal query
 * sets `retry: false` and one dropped request is enough to land here.
 */
export function MemberLoadError({
  title = "We couldn't load this",
  hint = 'Your training is safe — we just can’t reach the gym right now.',
  onRetry,
  isRetrying,
}: {
  title?: string
  hint?: string
  onRetry?: () => void
  isRetrying?: boolean
}) {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center">
      <span className="flex size-12 items-center justify-center rounded-full bg-muted">
        <CloudOff className="size-6 text-muted-foreground" />
      </span>
      <p className="font-medium">{title}</p>
      <p className="max-w-xs text-sm text-muted-foreground">{hint}</p>
      {onRetry && (
        <Button variant="outline" className="mt-2 h-11 rounded-xl px-4" onClick={onRetry} disabled={isRetrying}>
          <RotateCw className={isRetrying ? 'size-4 animate-spin' : 'size-4'} />
          {isRetrying ? 'Trying…' : 'Try again'}
        </Button>
      )}
    </div>
  )
}

/**
 * What each rung of the rank ladder means, in one line — shared so the celebration and the rank page
 * cannot drift apart. A member congratulated with one sentence and shown a different one on the page
 * behind it has been told two things about the same event.
 *
 * Every line says only what the ladder itself guarantees. The earlier copy asserted elapsed time and
 * gym-floor reputation — "months of showing up", "years of it", "the gym knows your name" — none of
 * which the app knows: RankPolicy converts lifetime XP into a tier and nothing else. A member who
 * trained hard for six weeks and crossed into Strong reads "half a year of it" and learns that the
 * app is guessing about them. What IS true of every rung is the XP it took, and the rank page prints
 * that from real data right beside this line.
 */
export const RANK_TIER_LINE: Record<RankTier, string> = {
  Newcomer: 'Everyone starts here. One session and you are moving.',
  Regular: 'Past the first milestone.',
  Committed: 'Well beyond where most first attempts stop.',
  Strong: 'A serious total. Under a quarter of a Legend.',
  Relentless: 'Past halfway to Elite.',
  Elite: 'The top of the ladder is in sight from here.',
  Titan: 'One rung from the end.',
  Legend: 'The top of the ladder. There is nothing above this.',
}

export const TIER_COLOR: Record<AchievementTier, string> = {
  Bronze: 'border-amber-700/40 text-amber-700',
  Silver: 'border-slate-400/50 text-slate-500',
  Gold: 'border-amber-500/50 text-amber-600',
  Platinum: 'border-cyan-500/50 text-cyan-600',
}

// Recovery status -> banner accent + friendly label. Fresh/Ready are "go" states; Fatigued and
// OvertrainingRisk escalate to warmer colours as advice to ease off.
export const RECOVERY_STYLE: Record<RecoveryStatus, { ring: string; text: string; label: string }> = {
  Fresh: { ring: 'border-emerald-500/40 bg-emerald-500/5', text: 'text-emerald-600', label: 'Fresh' },
  Ready: { ring: 'border-sky-500/40 bg-sky-500/5', text: 'text-sky-600', label: 'Ready' },
  Fatigued: { ring: 'border-amber-500/40 bg-amber-500/5', text: 'text-amber-600', label: 'Fatigued' },
  OvertrainingRisk: { ring: 'border-red-500/40 bg-red-500/5', text: 'text-red-600', label: 'Overtraining risk' },
}

// Recommendation type -> icon + accent color, so the card reads at a glance without parsing text.
export const RECOMMENDATION_STYLE: Record<RecommendationType, { icon: typeof Shuffle; text: string }> = {
  VolumeSuggestion: { icon: BarChart3, text: 'text-violet-600' },
  ExerciseSubstitution: { icon: Shuffle, text: 'text-emerald-600' },
  RecoveryAdvice: { icon: HeartPulse, text: 'text-red-600' },
  TrainerPlanActive: { icon: ClipboardList, text: 'text-indigo-600' },
}

// PR metric names come back as the enum name; give them friendly labels and units.
export const PR_TYPE_CONFIG: Record<string, { label: string; unit: string }> = {
  MaxWeight: { label: 'Max weight', unit: 'kg' },
  EstimatedOneRepMax: { label: 'Est. 1RM', unit: 'kg' },
  SessionVolume: { label: 'Best session volume', unit: 'kg' },
}

// Reasons come back from the API as the XpReason enum name; give members friendly labels.
export const XP_REASON_LABELS: Record<string, string> = {
  WorkoutCompleted: 'Workout logged',
  GymVisit: 'Gym visit',
  StreakMilestone: 'Streak milestone',
  ProgressiveImprovement: 'Progressive improvement',
  GoalCompleted: 'Goal completed',
  TrainerVerified: 'Trainer verified',
  NutritionAdherence: 'Nutrition on target',
  RecoveryLogged: 'Recovery logged',
  ChallengeCompleted: 'Challenge completed',
}

export const classTimeFormat = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  hour: 'numeric',
  minute: '2-digit',
  timeZone: 'UTC',
})

export function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <p className="font-medium">{title}</p>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

export function MacroBar({ label, consumed, target, unit }: { label: string; consumed: number; target: number | null; unit: string }) {
  const percent = target && target > 0 ? Math.min(100, Math.round((consumed / target) * 100)) : null

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="font-medium">{label}</span>
        <span className="text-muted-foreground">
          {Math.round(consumed)}
          {target ? ` / ${Math.round(target)}` : ''} {unit}
        </span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-muted">
        <div
          className="h-full rounded-full bg-primary transition-all"
          style={{ width: `${percent ?? Math.min(100, consumed > 0 ? 100 : 0)}%` }}
        />
      </div>
    </div>
  )
}

export function MasteryBar({ label, percent, hint }: { label: string; percent: number; hint?: string }) {
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2 text-sm">
        <span className="truncate">{label}</span>
        <span className="shrink-0 text-muted-foreground">
          {percent}%{hint ? ` · ${hint}` : ''}
        </span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${Math.min(100, Math.max(0, percent))}%` }} />
      </div>
    </div>
  )
}

export function ChallengeRow({ challenge }: { challenge: MyChallenge }) {
  const join = useJoinChallenge()
  const leave = useLeaveChallenge()
  const pending = join.isPending || leave.isPending

  const handleJoin = () =>
    join.mutate(challenge.id, {
      onSuccess: () => toast.success(challenge.isCompleted ? "You're in — challenge already complete!" : "You're in!"),
      onError: () => toast.error('Could not join the challenge.'),
    })

  const handleLeave = () =>
    leave.mutate(challenge.id, {
      onError: () => toast.error('Could not leave the challenge.'),
    })

  return (
    <div className="space-y-2 rounded-lg border p-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="flex items-center gap-1.5 text-sm font-medium">
            <Flag className="size-3.5 shrink-0 text-amber-500" />
            {challenge.name}
          </p>
          {challenge.description && <p className="text-xs text-muted-foreground">{challenge.description}</p>}
        </div>
        {challenge.isCompleted ? (
          <Badge variant="success" className="shrink-0">
            Completed
          </Badge>
        ) : challenge.joined ? (
          <Button size="sm" variant="outline" className="shrink-0" disabled={pending} onClick={handleLeave}>
            {leave.isPending && <Loader2 className="size-4 animate-spin" />}
            Leave
          </Button>
        ) : (
          <Button size="sm" className="shrink-0" disabled={pending} onClick={handleJoin}>
            {join.isPending && <Loader2 className="size-4 animate-spin" />}
            Join
          </Button>
        )}
      </div>
      {challenge.joined && (
        <MasteryBar
          label="Progress"
          percent={Math.min(100, Math.round((challenge.myWorkoutCount / challenge.targetWorkoutCount) * 100))}
          hint={`${challenge.myWorkoutCount} / ${challenge.targetWorkoutCount} workouts`}
        />
      )}
    </div>
  )
}

export const SUGGESTION_CONFIG: Record<OverloadSuggestion, { label: string; icon: typeof TrendingUp; variant: 'success' | 'warning' | 'secondary' | 'outline' }> = {
  Progressing: { label: 'Progressing', icon: TrendingUp, variant: 'success' },
  ReadyToIncreaseWeight: { label: 'Add weight next time', icon: Sparkles, variant: 'warning' },
  ConsiderDeload: { label: 'Consider a lighter session', icon: TrendingDown, variant: 'secondary' },
  InsufficientData: { label: 'Log again to get a suggestion', icon: Minus, variant: 'outline' },
}

export const dateFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' })
export const dateTimeFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' })

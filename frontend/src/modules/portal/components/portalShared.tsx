/**
 * Shared building blocks for the member portal pages. These used to live inside a single 801-line
 * MemberPortalPage; they were lifted out when that page was split into focused screens so the
 * styling of a mastery bar, a macro bar or a challenge row stays identical wherever it appears.
 */
import { BarChart3, ClipboardList, Flag, HeartPulse, Loader2, Minus, Shuffle, Sparkles, Target, TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import type { AchievementTier, OverloadSuggestion, RecommendationType, RecoveryStatus } from '@/modules/portal/api/portalApi'
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
export const RECOMMENDATION_STYLE: Record<RecommendationType, { icon: typeof Target; text: string }> = {
  PlateauAlert: { icon: TrendingUp, text: 'text-amber-600' },
  WeeklyFocus: { icon: Target, text: 'text-sky-600' },
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

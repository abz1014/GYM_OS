import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { Award, Flame, Loader2, Sparkles, Target, Trophy, TrendingUp, Undo2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { ActivityRing } from '@/shared/components/ActivityRing'
import { PR_TYPE_CONFIG } from '@/modules/portal/components/portalShared'
import { useOfferUndo, useUndoMyWorkout, type MyWorkoutResult } from '@/modules/portal/api/portalApi'

/** Reuses the labels the progress page already uses, so a record is named the same way everywhere. */
function formatRecord(type: string, value: number): string {
  const unit = PR_TYPE_CONFIG[type]?.unit ?? ''
  return `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 1 })}${unit ? ` ${unit}` : ''}`
}

/**
 * The moment after a member logs a session.
 *
 * Full-screen and celebratory on purpose. Logging is the one thing the app asks a member to do, and
 * it used to be answered by a toast that slid away in three seconds — the least memorable possible
 * response to the only effort they made. Everything the engine computed off that session is already
 * real; this just tells them, once, in the order they care about: the ring first (am I on track),
 * then what was exceptional (records, achievements, level), then the slower-burning things.
 *
 * One tap dismisses it. There is deliberately no second action competing with that.
 */
export function WorkoutCelebration({ result, onDismiss }: { result: MyWorkoutResult; onDismiss: () => void }) {
  const undo = useUndoMyWorkout()
  const offerUndo = useOfferUndo()
  const [undone, setUndone] = useState(false)

  /**
   * Closing this screen used to take the only way back with it: a member who tapped Done and then
   * realised had no recourse, however recent the mistake. The way out now carries the way back for
   * a few seconds, since whoever mis-taps the confirm is the same person likely to dismiss on
   * reflex. Nothing is offered after an undo — there is no session left to take back.
   */
  const onDone = useCallback(() => {
    if (!undone) offerUndo(result.workoutLogId, result.character)
    onDismiss()
  }, [undone, offerUndo, result.workoutLogId, result.character, onDismiss])

  // Escape dismisses too — the overlay covers the app, so it must never be a trap. It goes through
  // the same path as Done, or the fastest way out would be the one with no way back.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onDone()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onDone])

  // With nothing bigger to report, the session names itself — "Push day", "Back & Arms" — instead of
  // the generic "Session logged" it used to fall back to. It is the member's own training described
  // back to them, and it costs them no extra tap to say it.
  const headline = result.goalJustMet
    ? "That's your week"
    : result.leveledUp
      ? `Level ${result.level}`
      : result.newRecords.length > 0
        ? 'New personal record'
        : result.character

  // When a bigger moment takes the headline, what the session actually was still gets said.
  const kicker = headline === result.character ? null : result.character

  const subhead = result.goalJustMet
    ? `${result.sessionsThisWeek} of ${result.weeklySessionGoal} sessions — ring closed.`
    : result.goalMet
      ? 'Goal already met this week. This one is a bonus.'
      : `${Math.max(0, result.weeklySessionGoal - result.sessionsThisWeek)} to go this week.`

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Workout logged"
      className="fixed inset-0 z-50 flex flex-col overflow-y-auto bg-background"
    >
      <div className="mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center gap-6 px-6 py-10 text-center">
        <div className="space-y-1">
          {kicker && (
            <p className="text-xs font-semibold tracking-widest text-primary uppercase">{kicker}</p>
          )}
          <h2 className="text-3xl font-bold tracking-tight">{headline}</h2>
          <p className="text-sm text-muted-foreground">{subhead}</p>
        </div>

        <ActivityRing
          value={result.sessionsThisWeek}
          goal={result.weeklySessionGoal}
          colorClassName={result.goalMet ? 'text-emerald-500' : 'text-primary'}
        >
          <span className="text-5xl leading-none font-black tracking-tight tabular-nums">{result.sessionsThisWeek}</span>
          <span className="mt-1 text-xs text-muted-foreground">of {result.weeklySessionGoal}</span>
        </ActivityRing>

        {/* The two numbers that always exist, side by side. */}
        <div className="grid w-full grid-cols-2 gap-3">
          <Stat icon={<Sparkles className="size-5 text-amber-500" />} value={`+${result.xpEarned}`} label="XP earned" />
          <Stat
            icon={<Flame className={result.workoutStreakWeeks > 0 ? 'size-5 text-orange-500' : 'size-5 text-muted-foreground'} />}
            value={String(result.workoutStreakWeeks)}
            label="week streak"
          />
        </div>

        {result.leveledUp && (
          <Banner
            icon={<TrendingUp className="size-5" />}
            title={`You reached level ${result.level}`}
            tone="text-emerald-600 dark:text-emerald-400"
          />
        )}

        {result.newRecords.length > 0 && (
          <Section title={result.newRecords.length === 1 ? 'New record' : 'New records'}>
            {result.newRecords.map((r, i) => (
              <Row
                key={`${r.exerciseName}-${r.type}-${i}`}
                icon={<Trophy className="size-4 text-amber-500" />}
                left={r.exerciseName}
                sub={PR_TYPE_CONFIG[r.type]?.label ?? r.type}
                right={formatRecord(r.type, r.value)}
              />
            ))}
          </Section>
        )}

        {result.newAchievements.length > 0 && (
          <Section title={result.newAchievements.length === 1 ? 'Achievement unlocked' : 'Achievements unlocked'}>
            {result.newAchievements.map((a) => (
              <Row key={a.code} icon={<Award className="size-4 text-violet-500" />} left={a.name} sub={a.description} />
            ))}
          </Section>
        )}

        {result.challengeProgress.length > 0 && (
          <Section title="Challenges">
            {result.challengeProgress.map((c) => (
              <Row
                key={c.name}
                icon={<Target className={c.justCompleted ? 'size-4 text-emerald-500' : 'size-4 text-muted-foreground'} />}
                left={c.name}
                sub={c.justCompleted ? 'Completed!' : undefined}
                right={`${Math.min(c.workoutsLogged, c.targetWorkoutCount)}/${c.targetWorkoutCount}`}
              />
            ))}
          </Section>
        )}
      </div>

      {/* Sticky so the way out is always in reach, however much landed above. */}
      <div className="sticky bottom-0 w-full space-y-2 border-t bg-background px-6 pt-4 pb-[calc(1rem+env(safe-area-inset-bottom))]">
        {/*
          Closing the celebration used to take the only way back with it. The way out now carries
          the way back for a few seconds — the member who taps Done reflexively is the same one who
          mis-tapped in the first place.
        */}
        <Button className="mx-auto flex h-14 w-full max-w-md text-base" onClick={onDone}>
          Done
        </Button>

        {/*
          One tap makes logging easy and a mis-tap equally easy. Without a way back, an accidental
          confirmation mints XP and can register a record that never happened — corrupting exactly
          the numbers above. Kept quiet: it's the exception, not a second call to action.
        */}
        <Button
          variant="ghost"
          className="mx-auto flex h-11 w-full max-w-md text-sm text-muted-foreground"
          disabled={undo.isPending || undone}
          onClick={() =>
            undo.mutate(result.workoutLogId, {
              onSuccess: () => {
                setUndone(true)
                toast.success('Workout removed.')
                onDismiss()
              },
              onError: () => toast.error("That workout can no longer be undone."),
            })
          }
        >
          {undo.isPending ? <Loader2 className="size-4 animate-spin" /> : <Undo2 className="size-4" />}
          {undo.isPending ? 'Removing…' : "I didn't do this"}
        </Button>
      </div>
    </div>
  )
}

function Stat({ icon, value, label }: { icon: ReactNode; value: string; label: string }) {
  return (
    <div className="flex flex-col items-center gap-1 rounded-xl border p-4">
      {icon}
      <span className="text-3xl leading-none font-black tracking-tight tabular-nums">{value}</span>
      <span className="text-xs text-muted-foreground">{label}</span>
    </div>
  )
}

function Banner({ icon, title, tone }: { icon: ReactNode; title: string; tone: string }) {
  return (
    <div className={`flex w-full items-center justify-center gap-2 rounded-xl border p-3 font-medium ${tone}`}>
      {icon}
      {title}
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="w-full space-y-2 text-left">
      <h3 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{title}</h3>
      <div className="overflow-hidden rounded-xl border">{children}</div>
    </section>
  )
}

function Row({
  icon,
  left,
  sub,
  right,
}: {
  icon: ReactNode
  left: string
  sub?: string
  right?: string
}) {
  return (
    <div className="flex items-center gap-3 border-b p-3 last:border-b-0">
      <span className="shrink-0">{icon}</span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-medium">{left}</span>
        {sub && <span className="block truncate text-xs text-muted-foreground">{sub}</span>}
      </span>
      {right && <span className="shrink-0 text-sm font-semibold tabular-nums">{right}</span>}
    </div>
  )
}

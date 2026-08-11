import { Link } from 'react-router-dom'
import { ClipboardList, Dumbbell, HeartPulse, Lightbulb, NotebookPen } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { LogRecoveryDialog, RecoveryLoggedToday } from '@/modules/portal/components/LogRecoveryDialog'
import { MemberEmptyState, MemberLoadError, RECOMMENDATION_STYLE, RECOVERY_STYLE, SUGGESTION_CONFIG, SectionCard, dateFormat } from '@/modules/portal/components/portalShared'
import {
  useMyRecommendations,
  useMyRecovery,
  useMyWorkoutAssignments,
  useMyWorkoutLogs,
  useMyWorkoutSuggestions,
  type MyExerciseSuggestion,
} from '@/modules/portal/api/portalApi'

/**
 * The last performance, said in a way that is true of the exercise it describes.
 *
 * The list this replaces printed "Last: — kg x 33 reps" for a plank and "best 0kg" for a push-up,
 * because it ran every exercise through a template that assumes external weight. A dash and a zero
 * are not the same claim as "this is a bodyweight movement", and both read as broken data.
 *
 * Reps are a TOTAL across the session's sets, which the old wording also hid: "60 reps" of tricep
 * pushdown reads as one absurd set unless it says so.
 */
function lastPerformance(s: MyExerciseSuggestion) {
  const reps = `${s.lastTotalReps} reps total`
  return s.lastWeightKg === null ? `Bodyweight · ${reps}` : `${s.lastWeightKg} kg · ${reps}`
}

/**
 * One suggestion, with the rest reachable.
 *
 * Six equally-weighted suggestions is a menu, and a menu is work: the member has to read all of them
 * and decide which matters before they can act. The server now returns them worst-first by
 * ProgressiveOverloadPolicy.LeadPriority, so the first is the one worth doing something about and the
 * others are context rather than competition.
 *
 * The remainder stay on the page behind a disclosure instead of being deleted — they are real, and a
 * member mid-session may well want them. They just should not shout.
 */
function SuggestionList({ suggestions }: { suggestions: MyExerciseSuggestion[] }) {
  const [lead, ...rest] = suggestions
  const config = SUGGESTION_CONFIG[lead.suggestion]

  return (
    <div className="space-y-3">
      <div className="rounded-panel border border-border bg-muted/30 p-4">
        <Badge variant={config.variant}>{config.label}</Badge>
        <p className="mt-2 text-lg font-semibold tracking-tight">{lead.exerciseName}</p>
        <p className="text-sm text-muted-foreground">Last time: {lastPerformance(lead)}</p>
        {lead.suggestedNextWeightKg && (
          <p className="mt-1 text-sm font-medium text-primary">Try {lead.suggestedNextWeightKg} kg today</p>
        )}
      </div>

      {rest.length > 0 && (
        <details className="group">
          <summary className="cursor-pointer list-none text-sm text-muted-foreground hover:text-foreground">
            {rest.length} other exercise{rest.length === 1 ? '' : 's'} with a suggestion
            <span className="ml-1 inline-block transition-transform duration-(--duration-micro) group-open:rotate-90">
              ›
            </span>
          </summary>
          <ul className="mt-2 space-y-2">
            {rest.map((s) => (
              <li key={s.exerciseId} className="flex items-start justify-between gap-2 text-sm">
                <div className="min-w-0">
                  <p className="truncate font-medium">{s.exerciseName}</p>
                  <p className="text-xs text-muted-foreground">{lastPerformance(s)}</p>
                </div>
                <Badge variant={SUGGESTION_CONFIG[s.suggestion].variant} className="shrink-0">
                  {SUGGESTION_CONFIG[s.suggestion].label}
                </Badge>
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}

/**
 * "How should I train, and how did I train?" — the coaching half of the member portal: recovery
 * state, what to do next, and the record of what's been done. Progress-over-time lives on My
 * Progress; this page is about the current training decision.
 */
export default function MyTrainingPage() {
  const recovery = useMyRecovery()
  const recommendations = useMyRecommendations()
  const suggestions = useMyWorkoutSuggestions()
  const assignments = useMyWorkoutAssignments()
  const workouts = useMyWorkoutLogs()

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">My Training</h1>
          <p className="text-sm text-muted-foreground">Your recovery, what to train next, and your session history.</p>
        </div>
        <Button asChild size="sm">
          <Link to="/log-activity">
            <NotebookPen className="size-4" />
            Log a workout
          </Link>
        </Button>
      </div>

      {recovery.data && (
        <Card className={RECOVERY_STYLE[recovery.data.status].ring}>
          <CardHeader className="flex-row items-start justify-between gap-3 space-y-0">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base">
                <HeartPulse className={`size-4 ${RECOVERY_STYLE[recovery.data.status].text}`} />
                Recovery
                <span className={`text-sm font-semibold ${RECOVERY_STYLE[recovery.data.status].text}`}>
                  · {RECOVERY_STYLE[recovery.data.status].label}
                </span>
              </CardTitle>
              <p className="text-sm text-muted-foreground">{recovery.data.reason}</p>
            </div>
            {/* Absent, not disabled, once today is logged — the server accepts one log per day and
                silently returns the existing row, so offering the button again invites a tap that
                confirms success without creating anything. What was logged is shown below instead. */}
            {!recovery.data.today && <LogRecoveryDialog />}
          </CardHeader>
          <CardContent className="space-y-3">
            {recovery.data.today && <RecoveryLoggedToday today={recovery.data.today} />}

            <div className="flex flex-wrap gap-4 text-sm">
              <span>
                <span className="font-semibold">{recovery.data.sessionsLast7Days}</span>{' '}
                <span className="text-muted-foreground">sessions / 7d</span>
              </span>
              <span>
                <span className="font-semibold">{recovery.data.restDaysLast7Days}</span>{' '}
                <span className="text-muted-foreground">rest days / 7d</span>
              </span>
              {recovery.data.daysSinceLastWorkout !== null && (
                <span>
                  <span className="font-semibold">{recovery.data.daysSinceLastWorkout}</span>{' '}
                  <span className="text-muted-foreground">
                    {recovery.data.daysSinceLastWorkout === 1 ? 'day' : 'days'} since last workout
                  </span>
                </span>
              )}
            </div>
            {recovery.data.muscleGroups.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {recovery.data.muscleGroups.map((m) => (
                  <span
                    key={m.muscleGroup}
                    className={`rounded-full border px-2.5 py-1 text-xs ${RECOVERY_STYLE[m.status].ring} ${RECOVERY_STYLE[m.status].text}`}
                    title={m.reason}
                  >
                    {m.muscleGroup} · {RECOVERY_STYLE[m.status].label}
                  </span>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/*
        Everything this card said about adding weight or training a weak area came out in the Step 9
        review: the first was repeated verbatim by the suggestions list below, the second by the
        mastery bars beside it, and both are said better on the home screen where they're ranked
        against everything else. What's left is what nothing else here knows — the plan you're on,
        the next rung of a progression, a week that swung.
      */}
      {recommendations.data && recommendations.data.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <Lightbulb className="size-4 text-amber-500" />
              Worth knowing
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="space-y-3">
              {recommendations.data.map((rec, index) => {
                const style = RECOMMENDATION_STYLE[rec.type]
                const Icon = style.icon
                return (
                  <li key={`${rec.type}-${rec.exerciseId ?? index}`} className="flex items-start gap-3">
                    <Icon className={`mt-0.5 size-4 shrink-0 ${style.text}`} />
                    <div>
                      <p className="text-sm font-medium">{rec.title}</p>
                      <p className="text-sm text-muted-foreground">{rec.explanation}</p>
                    </div>
                  </li>
                )
              })}
            </ul>
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">What to train today</CardTitle>
          </CardHeader>
          <CardContent>
            {/*
              Four cards on this page read their data through `data && data.length > 0`, which a
              failed request satisfies in exactly the same way an untrained member does. Every query
              here sets `retry: false`, so one dropped packet used to print this card's "Log a couple
              of sessions to start getting suggestions." to someone who has logged fifty — with
              nothing on screen to tell them the sentence came from a broken connection.
            */}
            {suggestions.isLoading ? (
              <Skeleton className="h-32 w-full" />
            ) : suggestions.isError ? (
              <MemberLoadError
                title="We couldn't load your suggestions"
                onRetry={() => void suggestions.refetch()}
                isRetrying={suggestions.isFetching}
              />
            ) : suggestions.data && suggestions.data.length > 0 ? (
              <SuggestionList suggestions={suggestions.data} />
            ) : (
              <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
                <Dumbbell className="size-6" />
                Log a couple of sessions to start getting suggestions.
              </div>
            )}
          </CardContent>
        </Card>

        <SectionCard title="Assigned Workouts">
          {/*
            The worst of the four, because it is the only one that makes a claim about somebody else:
            a dropped request told the member their trainer had not written them a plan. There is no
            way to check that from this screen and every reason to believe it, and what follows is a
            conversation at the front desk about a plan that was there all along.
          */}
          {assignments.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : assignments.isError ? (
            <MemberLoadError
              title="We couldn't load your plan"
              onRetry={() => void assignments.refetch()}
              isRetrying={assignments.isFetching}
            />
          ) : assignments.data && assignments.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {assignments.data.map((a) => (
                <li key={a.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{a.workoutTemplateName}</p>
                    <p className="text-xs text-muted-foreground">
                      {dateFormat.format(new Date(a.startDate))}
                      {a.endDate ? ` – ${dateFormat.format(new Date(a.endDate))}` : ''}
                    </p>
                  </div>
                  {(!a.endDate || new Date(a.endDate) >= new Date()) && <Badge variant="success">Active</Badge>}
                </li>
              ))}
            </ul>
          ) : (
            <MemberEmptyState
              icon={ClipboardList}
              title="No plan from your trainer yet"
              hint="You don't need one to start — log whatever you do and it all counts."
              action={{ label: 'Log a workout', to: '/log-activity' }}
            />
          )}
        </SectionCard>

        <SectionCard title="Recent Sessions">
          {/*
            The same shape aimed at the member's own record: "Your training history starts here" was
            printed over a training history that already exists. The empty state below stays exactly
            as written, because for the member it was written for — the one on session zero — it is
            both true and the most useful thing this card can say.
          */}
          {workouts.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : workouts.isError ? (
            <MemberLoadError
              title="We couldn't load your sessions"
              onRetry={() => void workouts.refetch()}
              isRetrying={workouts.isFetching}
            />
          ) : workouts.data && workouts.data.length > 0 ? (
            <ul className="space-y-2 text-sm">
              {workouts.data.slice(0, 10).map((w) => (
                <li key={w.id} className="flex items-center justify-between gap-2 border-b pb-2 last:border-0">
                  <span className="truncate">{w.workoutTemplateName ?? w.character}</span>
                  <span className="shrink-0 text-muted-foreground">{dateFormat.format(new Date(w.loggedAt))}</span>
                </li>
              ))}
            </ul>
          ) : (
            <MemberEmptyState
              icon={Dumbbell}
              title="Your training history starts here"
              hint="Every session you log builds the picture — records, streaks and all."
              action={{ label: 'Log a workout', to: '/log-activity' }}
            />
          )}
        </SectionCard>
      </div>
    </div>
  )
}

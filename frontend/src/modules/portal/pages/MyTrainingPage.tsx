import { Link } from 'react-router-dom'
import { ClipboardList, Dumbbell, HeartPulse, Lightbulb, Moon, NotebookPen } from 'lucide-react'
import { toast } from 'sonner'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { MasteryBar, MemberEmptyState, RECOMMENDATION_STYLE, RECOVERY_STYLE, SUGGESTION_CONFIG, SectionCard, dateFormat } from '@/modules/portal/components/portalShared'
import {
  useLogMyRecovery,
  useMyMastery,
  useMyRecommendations,
  useMyRecovery,
  useMyWorkoutAssignments,
  useMyWorkoutLogs,
  useMyWorkoutSuggestions,
  type GroupMastery,
} from '@/modules/portal/api/portalApi'

/**
 * "How should I train, and how did I train?" — the coaching half of the member portal: recovery
 * state, what to do next, and the record of what's been done. Progress-over-time lives on My
 * Progress; this page is about the current training decision.
 */
export default function MyTrainingPage() {
  const recovery = useMyRecovery()
  const recommendations = useMyRecommendations()
  const suggestions = useMyWorkoutSuggestions()
  const mastery = useMyMastery()
  const assignments = useMyWorkoutAssignments()
  const workouts = useMyWorkoutLogs()
  const logRecovery = useLogMyRecovery()

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
            <Button
              size="sm"
              variant="outline"
              className="shrink-0"
              disabled={logRecovery.isPending}
              onClick={() =>
                logRecovery.mutate(
                  { kind: 'RestDay', notes: null },
                  { onSuccess: () => toast.success('Recovery day logged.') },
                )
              }
            >
              <Moon className="mr-1 size-4" />
              {logRecovery.isPending ? 'Logging…' : 'Log recovery day'}
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
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
            <CardTitle className="text-base">Workout Suggestions</CardTitle>
          </CardHeader>
          <CardContent>
            {suggestions.isLoading ? (
              <Skeleton className="h-32 w-full" />
            ) : suggestions.data && suggestions.data.length > 0 ? (
              <ul className="space-y-2.5">
                {suggestions.data.slice(0, 6).map((s) => {
                  const config = SUGGESTION_CONFIG[s.suggestion]
                  return (
                    <li key={s.exerciseId} className="flex items-start justify-between gap-2 text-sm">
                      <div className="min-w-0">
                        <p className="truncate font-medium">{s.exerciseName}</p>
                        <p className="text-xs text-muted-foreground">
                          Last: {s.lastWeightKg ?? '—'} kg × {s.lastTotalReps} reps
                          {s.suggestedNextWeightKg ? ` → try ${s.suggestedNextWeightKg} kg` : ''}
                        </p>
                      </div>
                      <Badge variant={config.variant} className="shrink-0">
                        {config.label}
                      </Badge>
                    </li>
                  )
                })}
              </ul>
            ) : (
              <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-muted-foreground">
                <Dumbbell className="size-6" />
                Log a couple of sessions to start getting suggestions.
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <Dumbbell className="size-4" />
              Mastery
            </CardTitle>
          </CardHeader>
          <CardContent>
            {mastery.data && mastery.data.exercises.length > 0 ? (
              <div className="space-y-4">
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Muscle groups</p>
                  {mastery.data.muscleGroups.map((g: GroupMastery) => (
                    <MasteryBar key={g.name} label={g.name} percent={g.masteryPercent} />
                  ))}
                </div>
                <div className="space-y-2">
                  <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Top exercises</p>
                  {mastery.data.exercises.slice(0, 5).map((e) => (
                    <MasteryBar
                      key={e.exerciseId}
                      label={e.exerciseName}
                      percent={e.masteryPercent}
                      hint={`${e.sessions} session${e.sessions === 1 ? '' : 's'} · best ${e.bestWeightKg}kg`}
                    />
                  ))}
                </div>
              </div>
            ) : (
              <p className="py-6 text-center text-sm text-muted-foreground">Train an exercise to build mastery.</p>
            )}
          </CardContent>
        </Card>

        <SectionCard title="Assigned Workouts">
          {assignments.isLoading ? (
            <Skeleton className="h-40 w-full" />
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
          {workouts.isLoading ? (
            <Skeleton className="h-40 w-full" />
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

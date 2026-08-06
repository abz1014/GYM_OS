import { CheckCircle2, Circle, Hourglass, MapPin } from 'lucide-react'

import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useMyPassport, type MyPassportEntry } from '@/modules/portal/api/portalApi'

/** Plain words for how long ago, because "41 days" is arithmetic and "6 weeks ago" is a memory. */
function lastTouched(days: number): string {
  if (days <= 0) return 'today'
  if (days === 1) return 'yesterday'
  if (days < 14) return `${days} days ago`
  if (days < 60) return `${Math.round(days / 7)} weeks ago`
  return `${Math.round(days / 30)} months ago`
}

function Row({ entry }: { entry: MyPassportEntry }) {
  return (
    <li className="flex items-start gap-3 border-b py-2 last:border-0">
      {entry.tried ? (
        <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-primary" />
      ) : (
        <Circle className="mt-0.5 size-4 shrink-0 text-muted-foreground/40" />
      )}
      <div className="min-w-0 flex-1">
        <p className={entry.tried ? 'truncate text-sm font-medium' : 'truncate text-sm text-muted-foreground'}>
          {entry.exerciseName}
        </p>
        <p className="text-xs text-muted-foreground">
          {entry.tried
            ? [
                `${entry.sessions} session${entry.sessions === 1 ? '' : 's'}`,
                // A movement with no load has no best weight; saying "0kg" would read as a failure.
                entry.bestWeightKg === null ? null : `best ${entry.bestWeightKg}kg`,
                // Stated as a fact, not a reprimand — it is their own record either way.
                entry.daysSinceLastTrained === null ? null : lastTouched(entry.daysSinceLastTrained),
              ]
                .filter(Boolean)
                .join(' · ')
            : 'Not tried yet'}
        </p>
      </div>
    </li>
  )
}

/**
 * The member's map of their own gym.
 *
 * Every other surface reports what they did; none can answer "what haven't I touched", because
 * mastery is built from the sessions that exist and a machine nobody has been near is invisible to
 * it. That absence is the point — this is the only screen that can send someone to a corner of the
 * gym they have walked past for a year.
 *
 * Nothing here is scored. The percentage is a count of the gym, not a mark out of ten for turning
 * up, and an untried movement reads as "not tried yet" rather than as something missed.
 */
export default function MyPassportPage() {
  const passport = useMyPassport()

  if (passport.isLoading) return <Skeleton className="mx-auto h-96 w-full max-w-2xl" />

  const data = passport.data
  if (!data) return null

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Gym Passport</h1>
        <p className="text-sm text-muted-foreground">
          {data.available === 0
            ? 'Your gym has not listed any equipment yet.'
            : data.complete
              ? "You've trained on everything your gym offers."
              : `You've used ${data.tried} of ${data.available} movements here.`}
        </p>
      </div>

      {data.available > 0 && (
        <Card>
          <CardContent className="flex items-center gap-4 py-5">
            <MapPin className="size-8 shrink-0 text-primary" />
            <div className="flex-1">
              <div className="flex items-baseline justify-between">
                <span className="text-3xl font-black tabular-nums">{data.percentCovered}%</span>
                <span className="text-sm text-muted-foreground">
                  {data.tried}/{data.available}
                </span>
              </div>
              <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-muted">
                <div className="h-full rounded-full bg-primary" style={{ width: `${data.percentCovered}%` }} />
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/*
        What v1 could not say. Once a member has tried everything, coverage is finished and the
        passport has nothing left — but they can still have quietly dropped something. Bounded to a
        few, worst first, so it stays a prompt rather than a backlog nobody reads.
      */}
      {data.goneQuiet.length > 0 && (
        <Card>
          <CardContent className="py-4">
            <div className="mb-2 flex items-center gap-2">
              <Hourglass className="size-4 shrink-0 text-muted-foreground" />
              <h2 className="text-xs font-semibold tracking-widest text-muted-foreground uppercase">
                Gone quiet
              </h2>
            </div>
            <ul className="space-y-1">
              {data.goneQuiet.map((e) => (
                <li key={e.exerciseId} className="flex items-baseline justify-between gap-3 text-sm">
                  <span className="truncate font-medium">{e.exerciseName}</span>
                  <span className="shrink-0 text-muted-foreground">
                    {lastTouched(e.daysSinceLastTrained ?? 0)}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {data.stamps.map((stamp) => (
        <Card key={stamp.equipment}>
          <CardContent className="py-4">
            <div className="mb-1 flex items-baseline justify-between gap-2">
              <h2 className="font-semibold">{stamp.equipment}</h2>
              <span className={stamp.complete ? 'text-xs font-medium text-primary' : 'text-xs text-muted-foreground'}>
                {stamp.complete ? 'All done' : `${stamp.tried}/${stamp.available}`}
              </span>
            </div>
            <ul>
              {stamp.entries.map((e) => (
                <Row key={e.exerciseId} entry={e} />
              ))}
            </ul>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

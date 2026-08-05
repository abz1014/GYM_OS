import { Flag } from 'lucide-react'

import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ChallengeRow } from '@/modules/portal/components/portalShared'
import { useMyChallenges } from '@/modules/challenges/api/challengesApi'

/** Community challenges on their own page — they were previously one card among 36. */
export default function MyChallengesPage() {
  const challenges = useMyChallenges()

  const active = challenges.data?.filter((c) => !c.isCompleted) ?? []
  const completed = challenges.data?.filter((c) => c.isCompleted) ?? []

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Challenges</h1>
        <p className="text-sm text-muted-foreground">Join a challenge and your logged workouts count automatically.</p>
      </div>

      {challenges.isLoading && <Skeleton className="h-48 w-full" />}

      {challenges.data && challenges.data.length === 0 && (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center text-sm text-muted-foreground">
            <Flag className="size-7" />
            No challenges running right now — check back soon.
          </CardContent>
        </Card>
      )}

      {active.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-sm font-medium text-muted-foreground">Open challenges</h2>
          {active.map((c) => (
            <ChallengeRow key={c.id} challenge={c} />
          ))}
        </section>
      )}

      {completed.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-sm font-medium text-muted-foreground">Completed</h2>
          {completed.map((c) => (
            <ChallengeRow key={c.id} challenge={c} />
          ))}
        </section>
      )}
    </div>
  )
}

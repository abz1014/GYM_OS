import { lazy, Suspense, useState } from 'react'
import { Flag, Trophy } from 'lucide-react'

import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

const LeaderboardPage = lazy(() => import('@/modules/portal/pages/LeaderboardPage'))
const MyChallengesPage = lazy(() => import('@/modules/portal/pages/MyChallengesPage'))

type Tab = 'leaderboard' | 'challenges'

const TABS: { key: Tab; label: string; icon: typeof Trophy }[] = [
  { key: 'leaderboard', label: 'Leaderboard', icon: Trophy },
  { key: 'challenges', label: 'Challenges', icon: Flag },
]

/**
 * The gym as other people, given somewhere to live.
 *
 * The leaderboard and the challenges were reachable from exactly one place in the whole app: two
 * rows in a "Community" group on the More screen, sitting below a full-bleed membership card and a
 * six-row Training group — which on a phone is below the fold. An exhaustive search for links to
 * either path found nothing else. A social feature nobody can navigate to is not a social feature,
 * and it was the owner's complaint almost word for word.
 *
 * They are one tab now rather than two, because they are one question — "how am I doing against
 * everyone else" — asked two ways, and because the tab bar has five slots and neither deserved a
 * whole one alone.
 *
 * The two pages are mounted as-is rather than merged. Both are complete screens with their own
 * loading, empty and failure states, and flattening them into one file would mean re-deriving all
 * six. Lazily, so opening Community does not pay for the tab the member did not pick.
 */
export default function CommunityPage() {
  const [tab, setTab] = useState<Tab>('leaderboard')

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        {TABS.map((t) => {
          const Icon = t.icon
          return (
            <button
              key={t.key}
              type="button"
              onClick={() => setTab(t.key)}
              aria-pressed={tab === t.key}
              className={cn(
                'press flex flex-1 items-center justify-center gap-2 rounded-2xl px-3 py-3 font-display text-sm font-bold transition-colors',
                tab === t.key
                  ? 'bg-primary text-primary-foreground shadow-volt'
                  : 'bg-muted text-muted-foreground hover:text-foreground',
              )}
            >
              <Icon className="size-4" />
              {t.label}
            </button>
          )
        })}
      </div>

      <Suspense fallback={<Skeleton className="h-64 w-full rounded-3xl shimmer" />}>
        {tab === 'leaderboard' ? <LeaderboardPage /> : <MyChallengesPage />}
      </Suspense>
    </div>
  )
}

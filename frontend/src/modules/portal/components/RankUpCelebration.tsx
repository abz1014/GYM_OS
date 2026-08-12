import { useEffect, useState } from 'react'
import { Sparkles } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { type MyRankPromotion, type RankTier } from '@/modules/portal/api/portalApi'
import { RANK_TIER_LINE } from '@/modules/portal/components/portalShared'

const TIER_GLOW: Record<RankTier, string> = {
  Newcomer: 'text-muted-foreground',
  Regular: 'text-sky-400',
  Committed: 'text-emerald-400',
  Strong: 'text-primary',
  Relentless: 'text-amber-400',
  Elite: 'text-orange-400',
  Titan: 'text-fuchsia-400',
  Legend: 'text-yellow-300',
}


/**
 * The moment.
 *
 * The system had exactly one celebration and it lived in a response body — the workout logger returned
 * LeveledUp, something flashed, and it was gone. Unrepeatable, and invisible to anyone who happened to
 * close the app first. This one is backed by a RankPromotion row, so the moment survives being missed:
 * it waits until the member actually opens the app, and the rung stays on their record afterwards.
 *
 * DISMISSAL IS EXPLICIT. No auto-timeout, because a member reading "very few members reach this" while
 * it fades out has been shown a reward and then had it taken away mid-sentence. It goes when they say
 * it goes.
 *
 * Motion is opt-out via prefers-reduced-motion — a full-screen animated takeover is exactly the case
 * that setting exists for, and a celebration that ignores it is a celebration that makes somebody feel
 * unwell.
 */
export function RankUpCelebration({
  promotion,
  onDismiss,
}: {
  promotion: MyRankPromotion
  onDismiss: () => void
}) {
  const tier = promotion.tier as RankTier
  const [shown, setShown] = useState(false)

  // One frame late so the entrance transition has a "from" state to animate out of; mounting straight
  // into the final state would snap.
  useEffect(() => {
    const id = requestAnimationFrame(() => setShown(true))
    return () => cancelAnimationFrame(id)
  }, [])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={`You reached ${tier}`}
      className={cn(
        'fixed inset-0 z-50 flex flex-col items-center justify-center gap-6 bg-background/95 p-6 text-center backdrop-blur-sm',
        'transition-opacity duration-(--duration-standard) ease-(--ease-uplift) motion-reduce:transition-none',
        shown ? 'opacity-100' : 'opacity-0',
      )}
    >
      <Sparkles
        className={cn(
          'size-10 transition-all duration-(--duration-expressive) ease-(--ease-uplift) motion-reduce:transition-none',
          TIER_GLOW[tier],
          shown ? 'scale-100 opacity-100' : 'scale-50 opacity-0',
        )}
      />

      <div className="space-y-2">
        <p className="text-[11px] font-bold tracking-[0.2em] text-muted-foreground uppercase">
          Rank up
        </p>
        <p
          className={cn(
            'font-display text-5xl font-black tracking-tight uppercase',
            TIER_GLOW[tier],
            'transition-all duration-(--duration-expressive) ease-(--ease-uplift) motion-reduce:transition-none',
            shown ? 'translate-y-0 opacity-100' : 'translate-y-3 opacity-0',
          )}
        >
          {tier}
        </p>
        <p className="mx-auto max-w-xs text-sm text-muted-foreground">{RANK_TIER_LINE[tier]}</p>
      </div>

      {/* The promise, stated at the moment it is most worth hearing. It is not marketing — PeakXp
          ratchets and RankPolicy reads the peak, so nothing in the system can take this rung back. */}
      <p className="mx-auto max-w-xs text-xs text-muted-foreground">
        This rank is yours permanently. Nothing takes it back.
      </p>

      <Button onClick={onDismiss} size="lg" className="mt-2 w-full max-w-xs">
        Nice
      </Button>
    </div>
  )
}

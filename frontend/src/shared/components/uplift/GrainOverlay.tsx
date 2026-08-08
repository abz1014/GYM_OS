import { cn } from '@/lib/utils'

/**
 * A 140×140 turbulence tile at 4–6% opacity, laid over a hero surface.
 *
 * Ships once, as one component, so the (fairly large) data URI exists a single time in the bundle
 * rather than once per screen that wants texture.
 *
 * **Hero surfaces only.** Never on list rows or tables: at row sizes the grain costs legibility and
 * buys nothing, and a table with texture behind 12pt type is harder to read for no gain.
 */
export function GrainOverlay({ className, opacity = 0.05 }: { className?: string; opacity?: number }) {
  return (
    <div
      aria-hidden
      className={cn('pointer-events-none absolute inset-0', className)}
      style={{
        opacity,
        backgroundImage:
          "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='140' height='140'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='140' height='140' filter='url(%23n)'/%3E%3C/svg%3E\")",
      }}
    />
  )
}

import { cn } from '@/lib/utils'

/**
 * A blurred radial sitting BEHIND a hero element — never on it.
 *
 * Rules the spec is firm about and this component enforces by construction: it is always
 * `aria-hidden` and `pointer-events-none`, and it is meant to bleed off the container edge, which is
 * what sells it. At most one per screen; two on login, where a cool counter-glow keeps the ink from
 * going muddy.
 *
 * Opacity is the dial: .10 for an ambient panel, .16 for a screen's hero, .30 for the personal-record
 * moment — the one place in the product where the interface is allowed to celebrate.
 */
export function Bloom({
  className,
  color = 'var(--primary)',
  opacity = 0.16,
  blur = 30,
  drift = false,
}: {
  className?: string
  color?: string
  opacity?: number
  blur?: number
  /** Slow ambient movement. Login only, and it stops under reduced-motion like everything else. */
  drift?: boolean
}) {
  return (
    <div
      aria-hidden
      className={cn('pointer-events-none absolute', drift && 'motion-safe:animate-[bloom-drift_14s_ease-in-out_infinite]', className)}
      style={{
        opacity,
        background: `radial-gradient(circle, ${color} 0%, transparent 68%)`,
        filter: `blur(${blur}px)`,
      }}
    />
  )
}
